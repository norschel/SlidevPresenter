// Run: node tests/SlideDevPresenter.Tests/Services/WebViewKeyboardBridgeScriptTests.cjs
// Execute the actual injected script with a minimal DOM; no browser or package dependencies.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname,
    '../../../src/SlideDevPresenter.App/Services/WebViewKeyboardBridge.cs'), 'utf8');
const script = source.match(/public const string Script = """\s*([\s\S]*?)\s*""" \+ EscapeScript;/)[1];

class Element {
    constructor(tag = 'div', contenteditable = null, parent = null) {
        this.tag = tag; this.contenteditable = contenteditable; this.parent = parent; this.style = {};
    }
    closest(selector) {
        assert.equal(selector, 'input,textarea,select,[contenteditable]:not([contenteditable="false"])');
        if (['input', 'textarea', 'select'].includes(this.tag)
            || (this.contenteditable !== null && this.contenteditable !== 'false')) return this;
        return this.parent?.closest(selector) ?? null;
    }
    appendChild(child) { this.child = child; }
    remove() { this.removed = true; }
}
function surface() {
    const messages = [], dispatched = [];
    const body = new Element();
    const window = { addEventListener() {} };
    const document = { body, createElement: () => new Element(), dispatchEvent: e => dispatched.push(e) };
    vm.runInNewContext(script, {
        window, document, Element, invokeCSharpAction: message => messages.push(message),
        KeyboardEvent: class { constructor(type, options) { this.type = type; Object.assign(this, options); } }
    });
    return { window, body, messages, dispatched };
}
function press(surface, key, target = surface.body, options = {}) {
    const event = {
        key, target, prevented: false, stopped: false,
        preventDefault() { this.prevented = true; },
        stopImmediatePropagation() { this.stopped = true; },
        ...options
    };
    surface.window.__sdpHandlePresentationKey(event);
    return event;
}
function editors() {
    return [new Element('input'), new Element('textarea'), new Element('select'),
        new Element('div', 'true'), new Element('span', null, new Element('div', ''))];
}

let cases = 0;
for (const color of ['black', 'white']) {
    for (const editable of editors()) {
        for (const options of [{}, { ctrlKey: true }, { metaKey: true }, { altKey: true }, { repeat: true }]) {
            const s = surface();
            s.window.__sdpSetBlankState(color);
            const overlay = s.body.child;
            const event = press(s, 'M', editable, options);
            assert.equal(event.prevented, false);
            assert.equal(event.stopped, false);
            assert.equal(overlay.removed, undefined);
            assert.deepEqual(s.messages, []);
            assert.deepEqual(s.dispatched, []);
            cases++;
        }
    }
    const s = surface();
    s.window.__sdpSetBlankState(color);
    assert.equal(press(s, 'm').prevented, true);
    assert.deepEqual(s.messages, ['sdp:cursor-toggle']);
    assert.equal(s.body.child.removed, undefined);
    // Preserve the prior any-key restore policy for editable keys other than M.
    assert.equal(press(s, 'n', new Element('input'), { ctrlKey: true }).prevented, true);
    assert.deepEqual(s.messages, ['sdp:cursor-toggle', 'sdp:restore']);
    assert.equal(s.body.child.removed, true);
    cases++;
    const nonEditable = surface();
    nonEditable.window.__sdpSetBlankState(color);
    assert.equal(press(nonEditable, 'm', new Element('div', 'false')).prevented, true);
    assert.deepEqual(nonEditable.messages, ['sdp:cursor-toggle']);
    cases++;
}
for (const [key, code] of Object.entries({
    ArrowLeft: 37, ArrowRight: 39, ArrowUp: 38, ArrowDown: 40,
    PageUp: 33, PageDown: 34, Backspace: 8, Enter: 13, ' ': 32
})) {
    const s = surface();
    assert.equal(press(s, key, s.body, { sdpForwarded: true, shiftKey: true }).prevented, true);
    assert.deepEqual(s.dispatched.map(e => [e.type, e.key, e.keyCode, e.shiftKey]),
        [['keydown', key, code, true], ['keyup', key, code, true]]);
    for (const target of editors()) {
        s.dispatched.length = 0;
        assert.equal(press(s, key, target, { sdpForwarded: true }).prevented, false);
        assert.deepEqual(s.dispatched, []);
    }
    cases++;
}
console.log(`WebView keyboard script: ${cases} cases passed.`);
