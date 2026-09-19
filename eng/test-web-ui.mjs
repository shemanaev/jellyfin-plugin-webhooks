import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

async function importModule(path) {
    const source = await readFile(new URL(path, import.meta.url), 'utf8');
    return import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
}

const editor = await importModule('../Jellyfin.Webhooks/Configuration/editor.js');
const configPage = await importModule('../Jellyfin.Webhooks/Configuration/configPage.js');

function createEditorView() {
    const elements = {
        '.form-webhook-editor': {
            resetCalled: false,
            reset() { this.resetCalled = true; },
        },
        '#hook-id': { value: 'old-id' },
        '#text-name': { value: 'Old name' },
        '#text-url': { value: 'https://old.example/webhook' },
        '#select-format': { value: 'Plex', selectedIndex: 2, options: [{}, {}, {}] },
        '#select-user': { value: 'old-user', selectedIndex: 1, options: [{}, {}] },
    };
    const eventInputs = [{ checked: true }, { checked: true }];
    return {
        elements,
        eventInputs,
        scrollTop: 100,
        scrollCoordinates: null,
        querySelector(selector) { return elements[selector]; },
        querySelectorAll(selector) {
            assert.equal(selector, '.events-container input[data-event]');
            return eventInputs;
        },
        scrollTo(x, y) { this.scrollCoordinates = [x, y]; },
    };
}

const editorView = createEditorView();
editor.resetEditor(editorView);
assert.equal(editorView.elements['.form-webhook-editor'].resetCalled, true);
assert.equal(editorView.elements['#hook-id'].value, '');
assert.equal(editorView.elements['#text-name'].value, '');
assert.equal(editorView.elements['#text-url'].value, '');
assert.equal(editorView.elements['#select-format'].selectedIndex, 0);
assert.equal(editorView.elements['#select-user'].selectedIndex, 0);
assert.deepEqual(editorView.eventInputs.map(input => input.checked), [false, false]);

editor.scrollEditorToTop(editorView);
assert.equal(editorView.scrollTop, 0);
assert.deepEqual(editorView.scrollCoordinates, [0, 0]);

const collected = editor.collectData({
    querySelector(selector) {
        return {
            '#hook-id': { value: 'hook-1' },
            '#text-name': { value: '  Living room  ' },
            '#text-url': { value: 'https://example.test/hook' },
            '#select-format': { value: 'Default' },
            '#select-user': { value: 'user-1' },
        }[selector];
    },
    querySelectorAll() {
        return [{ getAttribute: () => 'Play' }, { getAttribute: () => 'Stop' }];
    },
});
assert.deepEqual(collected, {
    Id: 'hook-1',
    Name: 'Living room',
    Url: 'https://example.test/hook',
    Format: 'Default',
    UserId: 'user-1',
    Events: ['Play', 'Stop'],
});

const namedHook = configPage.getHookHtml({
    Id: 'hook-1',
    Name: 'Living room <TV>',
    Url: 'https://example.test/hook?a=1&b=2',
    Format: 'Default',
    Events: ['Play', 'Stop'],
});
assert.match(namedHook, /Living room &lt;TV&gt;/);
assert.match(namedHook, /https:\/\/example\.test\/hook\?a=1&amp;b=2/);
assert.match(namedHook, /Default · Play, Stop/);

const legacyHook = configPage.getHookHtml({
    Id: 'hook-2',
    Url: 'https://legacy.example/hook',
    Format: 'Get',
    Events: ['Progress'],
});
assert.match(legacyHook, /https:\/\/legacy\.example\/hook/);
assert.match(legacyHook, /Progress/);

console.log('Web UI regression tests passed.');
