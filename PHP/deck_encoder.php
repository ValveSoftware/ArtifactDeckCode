<?php

require_once( 'commonutils.php' );

// Basic Deck encoder
class CArtifactDeckEncoder
{
	public static $s_nCurrentVersion = 2;
	private static $sm_rgchEncodedPrefix = "ADC";
	private static $sm_nMaxBytesForVarUint32 = 5;
	private static $knHeaderSize = 3;


	//expects array("heroes" => array(id, turn), "cards" => array(id, count), "name" => name)
	//	signature cards for heroes SHOULD NOT be included in "cards"
	public static function EncodeDeck( $deckContents )
	{
		if( !$deckContents )
			return false;

		$bytes = CArtifactDeckEncoder::EncodeBytes( $deckContents );
		if( !$bytes )
			return false;
		$deck_code = CArtifactDeckEncoder::EncodeBytesToString( $bytes );
		return $deck_code;
	}


	private static function EncodeBytes( $deckContents )
	{
		if( !isset($deckContents) || !isset($deckContents['heroes']) || !isset($deckContents['cards']) )
			return false;

		usort( $deckContents['heroes'], "CArtifactDeckEncoder::SortCardsById" );
		usort( $deckContents['cards'], "CArtifactDeckEncoder::SortCardsById" );

		$countHeroes = count( $deckContents['heroes'] );
		$allCards = array_merge( $deckContents['heroes'], $deckContents['cards'] );

		$bytes = array();
		//our version and hero count
		$version = CArtifactDeckEncoder::$s_nCurrentVersion << 4 | CArtifactDeckEncoder::ExtractNBitsWithCarry( $countHeroes, 3 );
		if( !CArtifactDeckEncoder::AddByte( $bytes, $version ) )
			return false;

		//the checksum which will be updated at the end
		$nDummyChecksum = 0;
		$nChecksumByte = count($bytes);
		if( !CArtifactDeckEncoder::AddByte( $bytes, $nDummyChecksum ) )
			return false;

		// write the name size
		$nameLen = 0;
		if( isset($deckContents['name']) )
		{
			// replace strip_tags() with your own HTML santizer or escaper.
			$name = strip_tags( $deckContents['name'] );
			$trimLen = strlen($name);
			while( $trimLen > 63 )
			{
				$amountToTrim = floor( ($trimLen - 63) / 4 );
				$amountToTrim = ($amountToTrim > 1) ? $amountToTrim : 1;
				$name = mb_substr( $name, 0, mb_strlen($name) - $amountToTrim );
				$trimLen = strlen($name);
			}

			$nameLen = strlen($name);
		}

		if( !CArtifactDeckEncoder::AddByte( $bytes, $nameLen ) )
			return false;

		if( !CArtifactDeckEncoder::AddRemainingNumberToBuffer( $countHeroes, 3, $bytes ) )
			return false;

		$prevCardId = 0;
		for( $unCurrHero = 0; $unCurrHero < $countHeroes; $unCurrHero++ )
		{
			$card = $allCards[ $unCurrHero ];
			if( $card['turn'] == 0 )
				return false;

			if( !CArtifactDeckEncoder::AddCardToBuffer( $card['turn'], $card['id'] - $prevCardId, $bytes ) )
				return false;

			$prevCardId = $card['id'];
		}

		//reset our card offset
		$prevCardId = 0;

		//now all of the cards
		for( $nCurrCard = $countHeroes; $nCurrCard < count($allCards); $nCurrCard++ )
		{
			//see how many cards we can group together
			$card = $allCards[$nCurrCard];
			if( $card['count'] == 0 )
				return false;
			if( $card['id'] <= 0 )
				return false;

			//record this set of cards, and advance
			if( !CArtifactDeckEncoder::AddCardToBuffer( $card['count'], $card['id'] - $prevCardId, $bytes ) )
				return false;
			$prevCardId = $card['id'];
		}

		// save off the pre string bytes for the checksum
		$preStringByteCount = count($bytes);

		//write the string
		{
			$nameBytes = unpack("C*", $name);
			foreach( $nameBytes as $nameByte )
			{
				if( !CArtifactDeckEncoder::AddByte( $bytes, $nameByte ) )
					return false;
			}
		}


		$unFullChecksum = CArtifactDeckEncoder::ComputeChecksum( $bytes, $preStringByteCount - CArtifactDeckEncoder::$knHeaderSize );
		$unSmallChecksum = ( $unFullChecksum & 0x0FF );

		$bytes[ $nChecksumByte ] = $unSmallChecksum;
		return $bytes;
	}

	private static function EncodeBytesToString( $bytes )
	{
		$byteCount = count($bytes);
		//if we have an empty buffer, just return
		if ( $byteCount == 0 )
			return false;

		$packed = pack( "C*", ...$bytes );
		$encoded = base64_encode( $packed );

		$deck_string = CArtifactDeckEncoder::$sm_rgchEncodedPrefix . $encoded;

		$replace = array('-','_');
		$search = array('/', '=');
		$fixedString = str_replace( $search, $replace, $deck_string );

		return $fixedString;
	}

	private static function SortCardsById( $a, $b )
	{
		return ( $a['id'] <= $b['id'] ) ? -1 : 1;
	}

	private static function ExtractNBitsWithCarry( $value, $numBits )
	{
		$unLimitBit = 1 << $numBits;
		$unResult = ( $value & ( $unLimitBit - 1 ) );
		if( $value >= $unLimitBit )
		{
			$unResult |= $unLimitBit;
		}

		return $unResult;
	}

	private static function AddByte( &$bytes, $byte )
	{
		if( $byte > 255 )
			return false;

		array_push( $bytes, $byte );
		return true;
	}

	//utility to write the rest of a number into a buffer. This will first strip the specified N bits off, and then write a series of bytes of the structure of 1 overflow bit and 7 data bits
	private static function AddRemainingNumberToBuffer( $unValue, $unAlreadyWrittenBits, &$bytes )
	{
		$unValue >>= $unAlreadyWrittenBits;
		$unNumBytes = 0;
		while ( $unValue > 0 )
		{
			$unNextByte = CArtifactDeckEncoder::ExtractNBitsWithCarry( $unValue, 7 );
			$unValue >>= 7;
			if( !CArtifactDeckEncoder::AddByte( $bytes, $unNextByte ) )
				return false;

			$unNumBytes++;
		}

		return true;
	}

	private static function AddCardToBuffer( $unCount, $unValue, &$bytes )
	{
		//this shouldn't ever be the case
		if( $unCount == 0 )
			return false;

		$countBytesStart = count($bytes);

		//determine our count. We can only store 2 bits, and we know the value is at least one, so we can encode values 1-5. However, we set both bits to indicate an 
		//extended count encoding
		$knFirstByteMaxCount = 0x03;
		$bExtendedCount = ( $unCount - 1 ) >= $knFirstByteMaxCount;

		//determine our first byte, which contains our count, a continue flag, and the first few bits of our value
		$unFirstByteCount = $bExtendedCount ? $knFirstByteMaxCount : /*( uint8 )*/( $unCount - 1 );
		$unFirstByte = ( $unFirstByteCount << 6 );
		$unFirstByte |= CArtifactDeckEncoder::ExtractNBitsWithCarry( $unValue, 5 );

		if( !CArtifactDeckEncoder::AddByte( $bytes, $unFirstByte ) )
			return false;
		
		//now continue writing out the rest of the number with a carry flag
		if( !CArtifactDeckEncoder::AddRemainingNumberToBuffer( $unValue, 5, $bytes ) )
			return false;

		//now if we overflowed on the count, encode the remaining count
		if ( $bExtendedCount )
		{
			if( !CArtifactDeckEncoder::AddRemainingNumberToBuffer( $unCount, 0, $bytes ) )
				return false;
		}

		$countBytesEnd = count($bytes);

		if( $countBytesEnd - $countBytesStart > 11 )
		{
			//something went horribly wrong
			return false;
		}

		return true;
	}

	private static function ComputeChecksum( &$bytes, $unNumBytes )
	{
		$unChecksum = 0;
		for ( $unAddCheck = CArtifactDeckEncoder::$knHeaderSize; $unAddCheck < $unNumBytes + CArtifactDeckEncoder::$knHeaderSize; $unAddCheck++ )
		{
			$byte = $bytes[$unAddCheck];
			$unChecksum += $byte;
		}

		return $unChecksum;
	}

}
