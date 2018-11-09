ArtifactDeckCode
---

This repository consists of 2 files that will assist in the encoding and decoding of deck 
codes. These are source examples in PHP that can be used as is or as reference for porting 
to other languages.  This README also contains an explanation of the card set API and 
corresponding json responses.

### Contents

* deck_encoder.php - the deck encoder used to convert deck info into a text code
* deck_decoder.php - the deck decoder used to take a text code and return the deck info

### About Deck Codes

Deck Codes are URL friendly base64 encoded strings that allow communication of deck contents.
They will always begin with the characters ADC followed by the encoded string.

The Artifact Website supports viewing of decks via a URL of the form:

```https://playartifact.com/d/<deck code >```

We encourage people to encode and generate a URL for sharing as this URL will show deck
previews on Twitter, Facebook, Steam and other applications which support Open Graph
and oEmbed.

### Example Deck Codes:
Example Green Black

```
https://playartifact.com/d/ADCJWkTZX05uwGDCRV4XQGy3QGLmqUBg4GQJgGLGgO7AaABRXhhbXBsZSBHcmVlbiBCbGFjaw__

Heroes:
1 Lycan
1 Rix
1 Phantom Assassin
1 Debbi the Cunning
1 Chen

Main Deck:
3 Pick Off
3 Payday
3 Steam Cannon
3 Mist of Avernus
3 Selemene's Favor
3 Iron Fog Goldmine
3 Untested Grunt
3 Thunderhide Pack
3 Revtel Convoy

Item Deck:
1 Apotheosis Blade
1 Horn of the Alpha
3 Poaching Knife
1 Red Mist Maul
1 Leather Armor
2 Traveler's Cloak
```

Example Red Blue

```
https://playartifact.com/d/ADCJcUQI30zuwEYg2ABeF1Bu94BmW4BSQu0AqUBlqQBiYeHRXhhbXBsZSBSZWQgQmx1ZQ__

Heroes:
1 Keefe the Bold
1 Luna
1 Bristleback
1 Zeus
1 Earthshaker

Main Deck:
3 Time of Triumph
3 Ventriloquy
3 Cunning Plan
3 Tower Barrage
3 New Orders
3 Clear the Deck
3 Foresight
3 Conflagration
3 Red Mist Pillager

Item Deck:
1 Red Mist Maul
2 Blade of the Vigil
2 Phase Boots
2 Leather Armor
2 Traveler's Cloak
```


Usage
---
The specification for the deck is a series of nested arrays in the structure below:
```

array
  heroes (array, expected size 5 with 3 heroes on turn 1, 1 hero on 2 and 1 hero on 3 )
    0 - (array)
      id - int
      turn - int
    ...

  cards (array)
    0 - (array)
      id - int
      count - int
    ...

  name - (string, will be clamped to 63 bytes)

```



Card Set API
---

If you're making a site that needs to show card information such as card images, names, and card
text, you will need to do a 2 stage request to our servers.  The first request is to request the
information for the set.  Current supported sets are 00 and 01.  You will need both to make get
all currently available cards.

First, make a request to the url of the form below where setid is 00 or 01. You will receieve 
a response similar to the one below.


```
https://playartifact.com/cardset/<setid>/

{
  "cdn_root": "https:\/\/<some host>\/",
  "url": "\/<some path>\/somefile.json",
  "expire_time": <unix timestamp>
}
```

After receiving the response, you must then request the JSON file at from the host specified.
In this example, https://some host/some path/somefile.json".  Please cache the
provided JSON for AT LEAST until the expire time provided.

The response will look similar to the one below for set 00
```
{
  "card_set": {
    "version": 1,
    "set_info": {
      "set_id": 0,
      "pack_item_def": 0,
      "name": {
        "english": "Base Set"
      }
    },
    "card_list": [{

      "card_id": 4000,
      "base_card_id": 4000,
      "card_type": "Hero",
      "card_name": {
        "english": "Farvhan the Dreamer"
      },
      "card_text": {
        "english": "Pack Leadership<BR>\nFarvhan the Dreamer's allied neighbors have +1 Armor."
      },
      "mini_image": {
        "default": "<url to png>"
      },
      "large_image": {
        "default": "<url to png>"
      },
      "ingame_image": {
        "default": "<url to png>"
      },
      "is_green": true,
      "attack": 4,
      "hit_points": 10,
      "references": [{
          "card_id": 4002,
          "ref_type": "includes",
          "count": 3
        },
        {
          "card_id": 4001,
          "ref_type": "passive_ability"
        }
      ]


    },
    ..... more cards ....

    ]
  }
}
```

### Some notes about the json format

* Currently only english is enabled, but more languages will be released at a later date
* Text fields (card_name, card_text and the name on set_info) will contain 
english and additional languages.
* Image fields (mini-image, large-image, ingame-image) provide a default image and keys 
for each supported language. 
* ref_type indicates a type of card reference:
  * "includes" - indicates a secondary card which will be automatically included into decks when the card is
added.  These should NOT be sent to the deck encoder.  The ref_type block will include the total count of these 
cards that will be added.
  * "references" - indicates that the card text mentions the specified card.
  * "passive_ability" - a passive ability 
  * "active_ability" - an ability which is activated by clicking on it.
