namespace ArtifactDeckTools
{
    #region Using
    
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    #endregion

    /// <summary>
    /// Used to decode a valid deck code for json retrieval.
    /// </summary>
    class ArtifactDeckDecoder
    {
        #region Variables

        public static int currentVersion = 2;
	    private static string encodedPrefix = "ADC";

        #endregion

        #region Methods

        /// <summary>
        /// Takes a valid deck code and returns a JObject of the related deck.
        /// </summary>
        /// <param name="strDeckCode"></param>
        /// <returns></returns>
        public static JObject ParseDeck( string strDeckCode )
        {
		    var deckBytes = DecodeDeckString( strDeckCode );
            if (deckBytes ==  null )
            {
                return null;
            }

		    var deck = ParseDeckInternal( ref deckBytes );
            return deck;
        }

        /// <summary>
        /// First strips off prefix and then reverses string cleanup and converts from Base64String to a list of bytes.
        /// </summary>
        /// <param name="strDeckCode"></param>
        /// <returns></returns>
        private static List<byte> DecodeDeckString( string strDeckCode )
        {
            //check for prefix
            if (strDeckCode.Substring( 0, encodedPrefix.Length) != encodedPrefix)
            {
                return null;
            }

		    //strip prefix from deck code
		    var strNoPrefix = strDeckCode.Substring(encodedPrefix.Length, strDeckCode.Length - encodedPrefix.Length);

		    // deck strings are base64 but with url compatible strings, put the URL special chars back
            strNoPrefix = strNoPrefix.Replace('-', '/').Replace('_', '=');
            List<byte> decoded = Convert.FromBase64String(strNoPrefix).ToList<byte>();
            return decoded;
        }

        /// <summary>
        /// Reads out a int encoded block of bits, returns true if another chunk should follow.
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="numBits"></param>
        /// <param name="currShift"></param>
        /// <param name="outBits"></param>
        /// <returns></returns>
        private static bool ReadBitsChunk( int chunk, int numBits, int currShift, ref int outBits )
        {
		    int continueBit = (1 << numBits );
		    int newBits = chunk & ( continueBit - 1 );
		    outBits |= ( newBits << currShift );

            return ( chunk & continueBit ) != 0;
        }

        /// <summary>
        /// Pulls next/requested Uint32 out of list of bytes
        /// </summary>
        /// <param name="baseValue"></param>
        /// <param name="baseBits"></param>
        /// <param name="data"></param>
        /// <param name="indexStart"></param>
        /// <param name="indexEnd"></param>
        /// <param name="outValue"></param>
        /// <returns></returns>
        private static bool ReadVarEncodedUint32( int baseValue, int baseBits, ref List<byte> data, ref int indexStart, int indexEnd, ref int outValue )
        {
		    outValue = 0;

		    int deltaShift = 0;
            if (( baseBits == 0 ) || ReadBitsChunk( baseValue, baseBits, deltaShift, ref outValue) )
		    {
			    deltaShift += baseBits;

                while (true)
                {
                    //do we have more room?
                    if ( indexStart >  indexEnd )
                    { 
                        return false;
                    }

				    //read the bits from this next byte and see if we are done
				    byte nextByte = data[indexStart++];
                    if (!ReadBitsChunk( nextByte, 7, deltaShift, ref outValue))
                    {
                        break;
                    }

				    deltaShift += 7;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles decoding a card that was serialized.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="indexStart"></param>
        /// <param name="indexEnd"></param>
        /// <param name="prevCardBase"></param>
        /// <param name="outCount"></param>
        /// <param name="outCardID"></param>
        /// <returns></returns>
        private static bool ReadSerializedCard( ref List<byte> data, ref int indexStart, int indexEnd, ref int prevCardBase, ref int outCount, ref int outCardID )
        {
            //end of the memory block?
            if ( indexStart > indexEnd )
            {
			    return false;
            }

		    //header contains the count (2 bits), a continue flag, and 5 bits of offset data. If we have 11 for the count bits we have the count
		    //encoded after the offset
		    byte header = data[indexStart++];
		    bool hasExtendedCount = (( header >> 6 ) == 0x03 );

		    //read in the delta, which has 5 bits in the header, then additional bytes while the value is set
		    int cardDelta = 0;
            if (!ReadVarEncodedUint32( header, 5, ref data,  ref indexStart, indexEnd, ref cardDelta))
            {
                return false;
            }

		    outCardID = prevCardBase + cardDelta;

                //now parse the count if we have an extended count
            if ( hasExtendedCount )
		    {
                if (!ReadVarEncodedUint32(0, 0, ref data, ref indexStart, indexEnd, ref outCount))
                {
                    return false;
                }
            }
		    else
		    {
			    //the count is just the upper two bits + 1 (since we don't encode zero)
			    outCount = ( header >> 6 ) + 1;

            }

		    //update our previous card before we do the remap, since it was encoded without the remap
		    prevCardBase = outCardID;

            return true;
        }

        /// <summary>
        /// Handles checks to make sure we are on the same version and can decode provided list of bytes.
        /// </summary>
        /// <param name="strDeckCode"></param>
        /// <param name="deckBytes"></param>
        /// <returns></returns>
        private static JObject ParseDeckInternal( ref List<byte> deckBytes )
        {
		    int currentByteIndex = 0;
		    int totalBytes = deckBytes.Count();

		    //check version num
		    int versionAndHeroes = deckBytes[currentByteIndex++];
		    int version = versionAndHeroes >> 4;
            if (currentVersion != version && version != 1 )
            {
                return null;
            }
			
		    //do checksum check
		    int checksum = deckBytes[currentByteIndex++];

		    int stringLength = 0;

            if ( version > 1)
            {
			    stringLength = deckBytes[currentByteIndex++];
            }

		    int totalCardBytes = totalBytes - stringLength;

            //grab the string size
			int computedChecksum = 0;
            for ( int i = currentByteIndex; i < totalCardBytes; i++ )
            {
				computedChecksum += deckBytes[i];
            }

			int masked = (computedChecksum & 0xFF);
            if ( checksum != masked )
            {
                return null;
            }

		    //read in our hero count (part of the bits are in the version, but we can overflow bits here
		    int numHeroes = 0;
            if (!ReadVarEncodedUint32(versionAndHeroes, 3, ref deckBytes, ref currentByteIndex, totalCardBytes, ref numHeroes))
            {
                return null;
            }


            //now read in the heroes
            var heroes = new List<JObject>();
			int prevCardBase = 0;
            for ( int currHero = 0; currHero < numHeroes; currHero++ )
			{
                var heroTurn = 0;
                var heroCardID = 0;
                if (!ReadSerializedCard( ref deckBytes, ref currentByteIndex, totalCardBytes, ref prevCardBase, ref heroTurn, ref heroCardID))
                {
                    return null;
                }
                heroes.Add(JObject.FromObject(new { id = heroCardID, turn = heroTurn }));
            }

            var cards = new List<JObject>();
		    prevCardBase = 0;
            while ( currentByteIndex < totalCardBytes )
		    {
                var cardCount = 0;
                var cardID = 0;
                if (!ReadSerializedCard(ref deckBytes, ref currentByteIndex, totalBytes, ref prevCardBase, ref cardCount, ref cardID))
                {
                    return null;
                }
                cards.Add(JObject.FromObject(new { id = cardID, count = cardCount }));
            }

		    var name = "";
            if ( currentByteIndex <= totalBytes )
		    {
                var bytes = deckBytes.GetRange(deckBytes.Count - stringLength, stringLength ).ToArray();
                
                name = Encoding.UTF8.GetString(bytes);

            }

            var deckList = new JObject();
            deckList.Add(new JProperty("heroes", heroes));
            deckList.Add(new JProperty("cards", cards));
            deckList.Add(new JProperty("name", name));

            return deckList; 
        }

        #endregion
    }
}
