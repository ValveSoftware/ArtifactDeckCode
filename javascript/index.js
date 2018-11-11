'use strict'

exports.parseDeck = parseDeck

const b64 = require('base64-js')

const CURRENT_VERSION = 2
const ENCODE_PREFIX = "ADC"

function parseDeck(deckCode) {
    if (!deckCode.startsWith(ENCODE_PREFIX)) throw "invalid deck code prefix"
    const b64Str = deckCode.substr(ENCODE_PREFIX.length)
        .replace(/-/g, "/")
        .replace(/_/g, "=")

    const deckCodeBytes = b64.toByteArray(b64Str)

    var byteIndex = 0

    const versionAndHeroes = deckCodeBytes[byteIndex++]
    const version = versionAndHeroes >> 4
    const checksum = deckCodeBytes[byteIndex++]
    const stringLength = version > 1 ? deckCodeBytes[byteIndex++] : 0

    if (version > CURRENT_VERSION) throw `deck code version ${version} is not supported`


    const nameStartIndex = deckCodeBytes.length - stringLength
    const deckBytes = deckCodeBytes.slice(byteIndex, nameStartIndex)
    const computedChecksum = computeChecksum(deckBytes)
    if (checksum != computedChecksum) throw "invalid deck code checksum"

    const decoder = cardDecoder(deckBytes)

    const heroesLength = decoder.readVar(versionAndHeroes, 3)
    const heroes = []
    for (let i = 0; i < heroesLength; i++) {
        heroes.push(decoder.readCard("turn"))
    }

    decoder.resetPreviousId()
    const cards = []
    while (decoder.hasNext()) {
        cards.push(decoder.readCard("count"))
    }

    const name = String.fromCharCode.apply(null, deckCodeBytes.slice(nameStartIndex))

    return { name, heroes, cards }
}

function cardDecoder(bytes) {
    const bytesLength = bytes.length
    var i = 0
    var previousId = 0

    function readVar(baseValue, baseBits) {
        var result = 0
        if (baseBits !== 0) {
            let continueBit = 1 << baseBits
            result = baseValue & (continueBit - 1)
            if ((baseValue & continueBit) === 0) return result
        }

        var currentShift = baseBits
        var currentByte
        do {
            if (i >= bytesLength) throw "invalid deck code"
            currentByte = bytes[i++]
            result |= ((currentByte & 127) << currentShift)
            currentShift += 7
        } while ((currentByte & 128) > 0)

        return result
    }

    function readCard(nName = "n") {
        if (i >= bytesLength) throw "invalid deck code"
        const header = bytes[i++]
        const id = previousId + readVar(header, 5)
        previousId = id
        var n = (header >> 6)
        if (n === 3) {
            n = readVar(0, 0) // n is higher than 3 and is encoded separately
        } else {
            n += 1 // adding 1 since zero values are not encoded here
        }

        return { id, [nName]: n }
    }

    return {
        readVar,
        readCard,
        hasNext: () => i < bytesLength,
        resetPreviousId: () => previousId = 0
    }
}

function computeChecksum(bytes) {
    return 0xFF & bytes.reduce((a, b) => a + b, 0)
}
