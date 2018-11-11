using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ArtifactDeckTools
{
    public class ArtifactDeckEncoder
    {
        public static byte currentVersion = 2;
	    private static byte maxBytesForVarUint32 = 5;
	    private static byte headerSize = 3;

	    private static string encodedPrefix = "ADC";

        //expects array("heroes" => array(id, turn), "cards" => array(id, count), "name" => name)
        //	signature cards for heroes SHOULD NOT be included in "cards"
        public static string EncodeDeck(JObject deckContents )
        {
            if (deckContents.Count == 0 )
            {
                return null;
            }

            var bytes = EncodeBytes( deckContents );
            if (bytes == null)
            {
                return null;
            }

		    var deckCode = EncodeBytesToString(ref bytes);

            return deckCode;
        }

        private static List<byte> EncodeBytes(JObject deckContents)
        {
            //Console.WriteLine(!deckContents.HasValues);
            if ( !deckContents.HasValues)
            {
                return null;
            }
            //Console.WriteLine(deckContents["heroes"].Count());
            if ( deckContents["heroes"].Count() == 0 )
            {
                return null;
            }
            //Console.WriteLine(deckContents["cards"].Count());
            if ( deckContents["cards"].Count() == 0 )
            {
                return null;
            }


            //var sortedDeck = deckContents;  //new JObject();
            var sortedHeroes = deckContents["heroes"].OrderBy(x => x["id"]);
            var sortedCards = deckContents["cards"].OrderBy(x => x["id"]);
            //sortedDeck.Add();
            //sortedDeck.Add(deckContents.SelectTokens("cards").OrderBy(x => x.SelectToken("id")));

            int countHeroes = sortedHeroes.Count();
            //allCards = array_merge( deckContents['heroes'], deckContents['cards']);

            var bytes = new List<byte>();

            //our version and hero count
            byte version = unchecked((byte)(unchecked((byte)(currentVersion << 4)) | ExtractNBitsWithCarry(countHeroes, (byte)3)));

            if (!AddByte(ref bytes, version))
            {
                return null;
            }

            //the checksum which will be updated at the end
            byte dummyChecksum = 0;
            int checksumByte = bytes.Count;
            if (!AddByte(ref bytes, dummyChecksum))
            {
                return null;
            }

            // write the name size
            int nameLen = 0;
            if (deckContents["name"] != null)
            {
                // replace strip_tags() with your own HTML santizer or escaper.
                var name = deckContents["name"].ToString();
                var trimLen = Encoding.UTF8.GetByteCount(name);
                while (trimLen > 63)
                {
                    int amountToTrim = (int)Math.Floor((trimLen - 63.0) / 4.0);
                    amountToTrim = (amountToTrim > 1) ? amountToTrim : 1;
                    name = name.Substring(0, amountToTrim);
                    trimLen = Encoding.UTF8.GetByteCount(name);
                }

                nameLen = Encoding.UTF8.GetByteCount(name);
            }

            if (!AddByte(ref bytes, (byte)nameLen))
            {
                return null;
            }

            if (!AddRemainingNumberToBuffer(countHeroes, 3, ref bytes))
            {
                return null;
            }
            
            int prevCardId = 0;

            foreach (var hero in sortedHeroes)
            {
                if ((byte)hero["turn"] == (byte)0)
                {
                    return null;
                }

                int heroId = (int)hero["id"];

                if (!AddCardToBuffer((byte)hero["turn"], heroId - prevCardId, ref bytes))
                {
                    return null;
                }

                prevCardId = heroId;
            }
            //reset our card offset
            prevCardId = 0;

            //now all of the cards
            foreach (var card in sortedCards)
            {
                //see how many cards we can group together
                if ((byte)card["count"] == (byte)0)
                {
                    return null;
                }
                if ((int)card["id"] <= 0)
                {
                    return null;
                }

                int cardId = (int)card["id"];
                //record this set of cards, and advance
                if (!AddCardToBuffer((byte)card["count"], cardId - prevCardId, ref bytes))
                {
                    return null;
                }

                prevCardId = cardId;
            }

            // save off the pre string bytes for the checksum
            byte preStringByteCount = (byte)bytes.Count;

            //write the string

            byte[] nameBytes = Encoding.UTF8.GetBytes(deckContents["name"].ToString());
            foreach ( byte cByte in nameBytes)
		    {
                if (!AddByte( ref bytes, cByte))
                {
                    return null;
                }
            }
                


		    int fullChecksum = ComputeChecksum( ref bytes, (byte)(preStringByteCount - headerSize));
            byte smallChecksum = unchecked((byte)( fullChecksum & 0x0FF ));

		    bytes[checksumByte] = smallChecksum;
            return bytes;
        }

        private static string EncodeBytesToString( ref List<byte> bytes )
        {
		    byte byteCount = (byte)bytes.Count;

            //if we have an empty buffer, just return
            if ( byteCount == 0 )
            {
                return null;
            }
            string byteString = Encoding.ASCII.GetString(bytes.ToArray<byte>());
            byte[] byteStringLatin = Encoding.Convert(Encoding.GetEncoding("ASCII"), Encoding.UTF8, bytes.ToArray<byte>());
            string encoded = Convert.ToBase64String(bytes.ToArray<byte>());

            //encoded = encoded);
		    string deck_string = encodedPrefix + encoded;

		    string fixedString = deck_string.Replace('/', '-').Replace('=', '_');

            return fixedString;
        }

        private static byte ExtractNBitsWithCarry(int nBits, byte carry)
        {
            byte limitBit = unchecked((byte)(1 << carry));
            byte minusOne = unchecked((byte)((int)limitBit - 1 ));
            byte result = unchecked((byte)(nBits & minusOne));
            if ( nBits >= limitBit )
		    {
                result |= limitBit;
            }

            return result;
        }

        private static bool AddByte( ref List<byte> bytes, byte addByte )
        {
            if (addByte > 255 )
            {
    			return false;
            }

            bytes.Add(addByte);
            return true;
        }

        //utility to write the rest of a number into a buffer. This will first strip the specified N bits off, and then write a series of bytes of the structure of 1 overflow bit and 7 data bits
        private static bool AddRemainingNumberToBuffer( int value, int alreadyWrittenBits, ref List<byte> bytes )
        {
            //Console.WriteLine(value);
            value = (value >> alreadyWrittenBits);
            //Console.WriteLine(value);
            int numBytes = 0;
            while ( value > 0 )
		    {
                byte nextByte = ExtractNBitsWithCarry( value, 7);
			    value >>= 7;
                    if (!AddByte( ref bytes, nextByte))
                        return false;

			    numBytes++;
            }
            return true;
        }

        private static bool AddCardToBuffer( byte count, int value, ref List<byte> bytes)
        {
            //this shouldn't ever be the case
            if ( count == 0 )
            {
                return false;
            }

		    byte countBytesStart = (byte)bytes.Count;

            //determine our count. We can only store 2 bits, and we know the value is at least one, so we can encode values 1-5. However, we set both bits to indicate an 
            //extended count encoding
            byte firstByteMaxCount = 0x03;
            bool extendedCount = ( count - 1 ) >= firstByteMaxCount;

            //determine our first byte, which contains our count, a continue flag, and the first few bits of our value
            byte firstByteCount = extendedCount ? firstByteMaxCount: (byte)( count - 1);
            byte firstByte = unchecked((byte)( firstByteCount << 6 ));
		    firstByte |= ExtractNBitsWithCarry( value, 5);

            if (!AddByte( ref bytes, firstByte))
            {
                return false;
            }


            //now continue writing out the rest of the number with a carry flag
            if (!AddRemainingNumberToBuffer( value, 5, ref bytes))
            {
                return false;
            }

            //now if we overflowed on the count, encode the remaining count
            if ( extendedCount )
		    {
                if (!AddRemainingNumberToBuffer( count, 0, ref bytes))
                {
                    return false;
                }
            }

		    byte countBytesEnd = (byte)bytes.Count;

            if ( countBytesEnd - countBytesStart > 11 )
		    {
                return false;
            }

            return true;
        }

        private static int ComputeChecksum( ref List<byte> bytes, byte numBytes )
        {
		    int checksum = 0;
            for ( byte addCheck = headerSize; addCheck < numBytes + headerSize; addCheck++ )
		    {
			    byte checkByte = bytes[addCheck];
			    checksum += checkByte;
                Console.WriteLine(checksum);
            }

            return checksum;
        }
    }
}
