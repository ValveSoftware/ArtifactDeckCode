using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArtifactDeckTools
{
    class ArtifactDeckDecoder
    {

        public static int currentVersion = 2;
	    private static string encodedPrefix = "ADC";

	    //returns array("heroes" => array(id, turn), "cards" => array(id, count), "name" => name)
	    public static JObject ParseDeck( string strDeckCode )
        {
		    var deckBytes = DecodeDeckString( strDeckCode);
            if (deckBytes ==  null )
            {
                return null;
            }

		    var deck = ParseDeckInternal( strDeckCode, ref deckBytes);
            return deck;
        }

        public static List<byte> RawDeckBytes( string strDeckCode )
        {
		    List<byte> deckBytes = DecodeDeckString( strDeckCode);
            return deckBytes;
        }

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

        //reads out a var-int encoded block of bits, returns true if another chunk should follow
        private static bool ReadBitsChunk( int chunk, int numBits, int currShift, ref int outBits )
        {
		    int continueBit = (1 << numBits );
		    int newBits = chunk & ( continueBit - 1 );
		    outBits |= ( newBits << currShift );

            return ( chunk & continueBit ) != 0;
        }

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

        //handles decoding a card that was serialized
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

        private static JObject ParseDeckInternal( string strDeckCode, ref List<byte> deckBytes )
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
            //deckList.Add(JProperty.FromObject(name));
            deckList.Add(new JProperty("name", name));
            //deckList.Add(heroes);
            //deckList.Add(cards);
            //deckList.Add(name);

            return deckList; //deckList;
            //return array("heroes" => $heroes, "cards" => $cards, "name" => $name);
        }
    }

    public class heroClass
    {
        int id;
        int turn;

        public heroClass(int id, int turn)
        { 
            this.id = id;
            this.turn = turn;
        }
    }

    public class cardClass
    {
        int id;
        int count;

        public cardClass(int id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }
}
