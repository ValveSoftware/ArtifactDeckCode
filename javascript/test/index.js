'use strict'

const assert = require('assert')
const { parseDeck, encodeDeck } = require('../')

const deck = require('./deck.json')
const DECK_CODE = "ADCJWkTZX05uwGDCRV4XQGy3QGLmqUBg4GQJgGLGgO7AaABR3JlZW4vQmxhY2sgRXhhbXBsZQ__"

describe('encodeDeck', function () {
    it('should encode a deck', function () {
        const result = encodeDeck(deck)
        assert.equal(result, DECK_CODE)
    })

    it('should encode a deck with unordered cards and heroes', function () {
        const newDeck = {
            name: deck.name,
            heroes: deck.heroes.slice(0).reverse(),
            cards: deck.cards.slice(0).reverse()
        }
        const result = encodeDeck(deck)
        assert.equal(result, DECK_CODE)
    })
})

describe('parseDeck', function () {
    it('should decode v2 deck code', function () {
        const result = parseDeck(DECK_CODE)
        assert.equal(result.name, deck.name)
        assert.equal(areTheCardArraysEqual(result.heroes, deck.heroes), true)
        assert.equal(areTheCardArraysEqual(result.cards, deck.cards), true)
    })

    it('should decode v1 deck code', function () {
        const result = parseDeck("ADCFWllfTm7AYMJFXhdAbLdAYuapQGDgZAmAYsaA7sBoAE_")
        assert.equal(result.name, "")
        assert.equal(areTheCardArraysEqual(result.heroes, deck.heroes), true)
        assert.equal(areTheCardArraysEqual(result.cards, deck.cards), true)
    })
})


function areTheCardArraysEqual(a, b) {
    if (a.length !== b.length) return false
    a.sort((a, b) => a.id - b.id)
    b.sort((a, b) => a.id - b.id)

    for (let i = 0, n = a.length; i < n; i++) {
        if (!areTheObjectsEqual(a[i], b[i])) return false
    }

    return true
}

function areTheObjectsEqual(a, b) {
    const ak = Object.keys(a)
    const bk = Object.keys(b)
    if (ak.length !== bk.length) return false

    for (let i = 0, n = ak.length; i < n; i++) {
        if (a[ak[i]] !== b[bk[i]]) return false
    }

    return true
}
