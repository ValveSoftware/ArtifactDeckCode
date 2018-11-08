ArtifactDeckCode
---

This repository consists of 2 files that will assist in the encoding and decoding of deck 
codes. These are source examples in PHP that can be used as is or as reference for porting 
to other languages.

### Contents

* deck_encoder.php - the deck encoder used to convert a deck into into text code
* deck_decoder.php - the deck decoder used to take a text code and return the deck info


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
      count - in
    ...

  name - (string, will be clamped to 63 bytes)

```



Card Data API
---

If you're making a site that needs to show card information such as card images, names, and card
text, you will need to do a 2 stage request to our servers.  The first request is to request the
information for the set.  Current supported sets are 00 and 01.  You will need both to make get
all currently available card.

First do a call to https://playartifact.com/cardset/setid/.  where setid is 00 or 01 You will receieve 
a response similar to the one below.


```
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
        "english": "Pack Leadership<BR>\n{s:thisCardName}'s allied neighbors have +1 Armor."
      },
      "mini_image": {
        "default": "<url to png>"
      },
      "large_image": {
        "default": "<url to png>"
      },
      "ingame_image": {
        "default": "<url to png>
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
          "card_id": 4002,
          "ref_type": "references"
        },
        {
          "card_id": 4001,
          "ref_type": "references"
        }
      ]


    },
    ..... more cards ....

    ]
  }
}
```

### Some Notes about the json format

* Currently only english is enabled, but more languages will be released at a later date
* Text fields (card_name, card_text and the name on set_info) will contain 
english and additional languages.
* Image fields (mini-image, large-image, ingame-image) provide a default image and keys 
for each supported language.  
* "ref_type" : "includes" specify cards which get auto included into decks when the card is
added.  These should NOT be sent to the deck encoder.
* "ref_type" : "references" typically refer to hero abilities or other special effects a card
may have.  These should NOT be sent to the deck encoder.
