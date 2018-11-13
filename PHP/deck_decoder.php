<?php

// Basic Deck decoder
class CArtifactDeckDecoder
{
	public static $s_nCurrentVersion = 2;
	private static $sm_rgchEncodedPrefix = "ADC";

	//returns array("heroes" => array(id, turn), "cards" => array(id, count), "name" => name)
	public static function ParseDeck( $strDeckCode )
	{
		$deckBytes = CArtifactDeckDecoder::DecodeDeckString( $strDeckCode );
		if( !$deckBytes )
			return false;

		$deck = CArtifactDeckDecoder::ParseDeckInternal( $strDeckCode, $deckBytes );
		return $deck;
	}

	public static function RawDeckBytes( $strDeckCode )
	{
		$deckBytes = CArtifactDeckDecoder::DecodeDeckString( $strDeckCode );
		return $deckBytes;
	}


	private static function DecodeDeckString( $strDeckCode )
	{
		//check for prefix
		if(substr($strDeckCode, 0, strlen(CArtifactDeckDecoder::$sm_rgchEncodedPrefix)) != CArtifactDeckDecoder::$sm_rgchEncodedPrefix)
			return false;

		//strip prefix from deck code
		$strNoPrefix = substr( $strDeckCode, strlen(CArtifactDeckDecoder::$sm_rgchEncodedPrefix) );

		// deck strings are base64 but with url compatible strings, put the URL special chars back
		$search = array('-','_');
		$replace = array('/', '=');
		$strNoPrefix = str_replace( $search, $replace, $strNoPrefix );
		$decoded = base64_decode( $strNoPrefix );
		return unpack("C*", $decoded);
	}


	//reads out a var-int encoded block of bits, returns true if another chunk should follow
	private static function ReadBitsChunk( $nChunk, $nNumBits, $nCurrShift, &$nOutBits )
	{
		$nContinueBit = ( 1 << $nNumBits );
		$nNewBits = $nChunk & ( $nContinueBit - 1 );
		$nOutBits |= ( $nNewBits << $nCurrShift );

		return ( $nChunk & $nContinueBit ) != 0;
	}

	private static function ReadVarEncodedUint32( $nBaseValue, $nBaseBits, $data, &$indexStart, $indexEnd, &$outValue )
	{
		$outValue = 0;

		$nDeltaShift = 0;
		if ( ( $nBaseBits == 0 ) || CArtifactDeckDecoder::ReadBitsChunk( $nBaseValue, $nBaseBits, $nDeltaShift, $outValue ) )
		{
			$nDeltaShift += $nBaseBits;

			while ( 1 )
			{
				//do we have more room?
				if ( $indexStart > $indexEnd )
					return false;

				//read the bits from this next byte and see if we are done
				$nNextByte = $data[$indexStart++];
				if ( !CArtifactDeckDecoder::ReadBitsChunk( $nNextByte, 7, $nDeltaShift, $outValue ) )
					break;

				$nDeltaShift += 7;
			}
		}

		return true;
	}

		
	//handles decoding a card that was serialized
	private static function ReadSerializedCard( $data, &$indexStart, $indexEnd, &$nPrevCardBase, &$nOutCount, &$nOutCardID )
	{
		//end of the memory block?
		if( $indexStart > $indexEnd )
			return false;

		//header contains the count (2 bits), a continue flag, and 5 bits of offset data. If we have 11 for the count bits we have the count
		//encoded after the offset
		$nHeader = $data[$indexStart++];
		$bHasExtendedCount = ( ( $nHeader >> 6 ) == 0x03 );

		//read in the delta, which has 5 bits in the header, then additional bytes while the value is set
		$nCardDelta = 0;
		if ( !CArtifactDeckDecoder::ReadVarEncodedUint32( $nHeader, 5, $data, $indexStart, $indexEnd, $nCardDelta ) )
			return false;

		$nOutCardID = $nPrevCardBase + $nCardDelta;

		//now parse the count if we have an extended count
		if ( $bHasExtendedCount )
		{
			if ( !CArtifactDeckDecoder::ReadVarEncodedUint32( 0, 0, $data, $indexStart, $indexEnd, $nOutCount ) )
				return false;
		}
		else
		{
			//the count is just the upper two bits + 1 (since we don't encode zero)
			$nOutCount = ( $nHeader >> 6 ) + 1;
		}

		//update our previous card before we do the remap, since it was encoded without the remap
		$nPrevCardBase = $nOutCardID;
		return true;
	}

	// $deckBytes will be 1 indexed (due to unpack return value).  If you are using 0 based indexing
	//	for your byte array, be sure to adjust appropriate below (see // 1 indexed)
	private static function ParseDeckInternal( $strDeckCode, $deckBytes )
	{
		$nCurrentByteIndex = 1;
		$nTotalBytes = count($deckBytes);

		//check version num
		$nVersionAndHeroes = $deckBytes[$nCurrentByteIndex++];
		$version = $nVersionAndHeroes >> 4;
		if( CArtifactDeckDecoder::$s_nCurrentVersion != $version && $version != 1 )
			return false;

		//do checksum check
		$nChecksum = $deckBytes[$nCurrentByteIndex++];

		$nStringLength = 0;
		if( $version > 1)
			$nStringLength = $deckBytes[$nCurrentByteIndex++];
		$nTotalCardBytes = $nTotalBytes - $nStringLength;

		//grab the string size
		{
			$nComputedChecksum = 0;
			for( $i = $nCurrentByteIndex; $i <= $nTotalCardBytes; $i++ )
				$nComputedChecksum += $deckBytes[$i];

			$masked = ($nComputedChecksum & 0xFF);
			if( $nChecksum != $masked )
				return false;
		}



		//read in our hero count (part of the bits are in the version, but we can overflow bits here
		$nNumHeroes = 0;
		if ( !CArtifactDeckDecoder::ReadVarEncodedUint32( $nVersionAndHeroes, 3, $deckBytes, $nCurrentByteIndex, $nTotalCardBytes, $nNumHeroes ) )
			return false;

		//now read in the heroes
		$heroes = array();
		{
			$nPrevCardBase = 0;
			for( $nCurrHero = 0; $nCurrHero < $nNumHeroes; $nCurrHero++ )
			{
				$nHeroTurn = 0;
				$nHeroCardID = 0;
				if( !CArtifactDeckDecoder::ReadSerializedCard( $deckBytes, $nCurrentByteIndex, $nTotalCardBytes, $nPrevCardBase, $nHeroTurn, $nHeroCardID ) )
				{
					return false;
				}

				array_push( $heroes, array("id" => $nHeroCardID, "turn" => $nHeroTurn) );
			}
		}

		$cards = array();
		$nPrevCardBase = 0;
		// 1 indexed - change to $nCurrentByteIndex < $nTotalCardBytes if 0 indexed
		while( $nCurrentByteIndex <= $nTotalCardBytes )
		{
			$nCardCount = 0;
			$nCardID = 0;
			if( !CArtifactDeckDecoder::ReadSerializedCard( $deckBytes, $nCurrentByteIndex, $nTotalBytes, $nPrevCardBase, $nCardCount, $nCardID ) )
				return false;

			array_push( $cards, array("id" => $nCardID, "count" => $nCardCount) );
		}

		$name = "";
		if( $nCurrentByteIndex <= $nTotalBytes )
		{
			$bytes = array_slice($deckBytes, -1 * $nStringLength);
			$name = implode(array_map("chr", $bytes));
			// replace strip_tags with an HTML sanitizer or escaper as needed.
			$name = strip_tags( $name );
		}

		return array("heroes" => $heroes, "cards" => $cards, "name" => $name);
	}
};

