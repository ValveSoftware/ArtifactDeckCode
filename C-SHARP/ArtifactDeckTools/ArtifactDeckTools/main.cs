using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArtifactDeckTools
{
    class main
    {
        public static void Main()
        {
            
            string deckList = "{\"heroes\":[{\"id\":4005,\"turn\":2},{\"id\":10014,\"turn\":1},{\"id\":10017,\"turn\":3},{\"id\":10026,\"turn\":1},{\"id\":10047,\"turn\":1}],\"cards\":[{\"id\":3000,\"count\":2},{\"id\":3001,\"count\":1},{\"id\":10091,\"count\":3},{\"id\":10102,\"count\":3},{\"id\":10128,\"count\":3},{\"id\":10165,\"count\":3},{\"id\":10168,\"count\":3},{\"id\":10169,\"count\":3},{\"id\":10185,\"count\":3},{\"id\":10223,\"count\":1},{\"id\":10234,\"count\":3},{\"id\":10260,\"count\":1},{\"id\":10263,\"count\":1},{\"id\":10322,\"count\":3},{\"id\":10354,\"count\":3}],\"name\":\"Green/Black Example\"}";

            string deckUrl = "ADCJWkTZX05uwGDCRV4XQGy3QGLmqUBg4GQJgGLGgO7AaABR3JlZW4vQmxhY2sgRXhhbXBsZQ__";
            

            var decoder = ArtifactDeckDecoder.ParseDeck(deckUrl);
            var encoder = ArtifactDeckEncoder.EncodeDeck(JObject.Parse(deckList));
            
            //decoder = ArtifactDeckDecoder.ParseDeck(encoder);
            //encoder = ArtifactDeckEncoder.EncodeDeck(decoder);


            Console.WriteLine($"{decoder}");
            Console.WriteLine($"{encoder}");
            Console.ReadKey();
        }
    }
}
