/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Book uploads: double click (double tap) on the header logo opens the file dialog.
 * Uploaded books have a delete button on their cover.
 * The page includes this script only when uploads are enabled on the server.
 */

(function () {
    var script = document.currentScript;
    var maxBytes = parseInt(script.getAttribute('data-max-mb'), 10) * 1024 * 1024;
    var texts = {
        tooLarge: script.getAttribute('data-too-large'),
        uploading: script.getAttribute('data-uploading'),
        failed: script.getAttribute('data-failed')
    };

    function showMessage(text, isError) {
        var box = document.getElementById('upload-message');
        if (!box) {
            box = document.createElement('div');
            box.id = 'upload-message';
            box.style.cssText = 'position:fixed;left:50%;bottom:24px;transform:translateX(-50%);max-width:90%;' +
                'padding:12px 18px;border-radius:8px;color:#fff;font-size:15px;z-index:10000;' +
                'box-shadow:0 2px 10px rgba(0,0,0,.3);transition:opacity .3s';
            document.body.appendChild(box);
        }
        box.style.background = isError ? '#c0392b' : '#2c3e50';
        box.style.opacity = '1';
        box.textContent = text;
        clearTimeout(box.hideTimer);
        box.hideTimer = setTimeout(function () { box.style.opacity = '0'; }, 5000);
    }

    function upload(files, index, added) {
        if (index >= files.length) {
            // Refresh the page to show the new books in the lists
            if (added) setTimeout(function () { location.reload(); }, 2500);
            return;
        }
        var file = files[index];
        if (file.size > maxBytes) {
            showMessage(file.name + ': ' + texts.tooLarge, true);
            upload(files, index + 1, added);
            return;
        }
        showMessage(texts.uploading + ' ' + file.name);
        fetch('/upload?name=' + encodeURIComponent(file.name), { method: 'POST', body: file, credentials: 'same-origin' })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                showMessage(file.name + ': ' + result.message, !result.ok);
                upload(files, index + 1, added || result.ok);
            })
            .catch(function () {
                showMessage(file.name + ': ' + texts.failed, true);
                upload(files, index + 1, added);
            });
    }

    function setup() {
        var logo = document.querySelector('.header-icon');
        var link = logo ? logo.closest('a') : null;
        if (!link) return;

        var input = document.createElement('input');
        input.type = 'file';
        input.multiple = true;
        // No file type filter: Android turns the extensions into MIME types and drops the unknown ones
        // (FB2), so the picker hides such files; the server checks the files anyway
        input.style.display = 'none';
        input.addEventListener('change', function () {
            var files = Array.prototype.slice.call(input.files);
            input.value = '';
            if (files.length) upload(files, 0, false);
        });
        document.body.appendChild(input);

        // A single click still opens the home page, after a short wait for the second click
        var timer = null;
        link.addEventListener('click', function (e) {
            e.preventDefault();
            if (timer) {
                clearTimeout(timer);
                timer = null;
                input.click();
            } else {
                timer = setTimeout(function () {
                    timer = null;
                    location.href = link.href;
                }, 350);
            }
        });
    }

    // Uploaded books are deleted by the button on their cover, after a question in a dialog of the page
    var deleteTexts = {
        question: script.getAttribute('data-delete-question'),
        confirm: script.getAttribute('data-delete'),
        cancel: script.getAttribute('data-cancel'),
        failed: script.getAttribute('data-delete-failed')
    };

    function addDialogStyles() {
        var style = document.createElement('style');
        style.textContent =
            // No inset and flex gap: older mobile browsers don't know them and the dialog would have no size
            '.confirm-overlay{position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,.45);display:flex;align-items:center;' +
            'justify-content:center;padding:16px;z-index:10001;opacity:0;transition:opacity .15s}' +
            '.confirm-overlay.shown{opacity:1}' +
            '.confirm-box{background:#fff;color:#222;border-radius:14px;padding:22px 22px 18px;width:100%;max-width:380px;' +
            'box-shadow:0 12px 40px rgba(0,0,0,.3);transform:scale(.95);transition:transform .15s}' +
            '.confirm-overlay.shown .confirm-box{transform:scale(1)}' +
            '.confirm-question{font-size:17px;font-weight:600;margin:0 0 8px}' +
            '.confirm-title{font-size:15px;color:#555;margin:0 0 20px;overflow-wrap:anywhere}' +
            '.confirm-buttons{display:flex;justify-content:flex-end}.confirm-delete{margin-left:10px}' +
            '.confirm-buttons button{font:inherit;font-size:15px;padding:9px 18px;border-radius:8px;border:0;cursor:pointer}' +
            '.confirm-cancel{background:#eceff1;color:#333}' +
            '.confirm-delete{background:#c0392b;color:#fff}' +
            '.confirm-cancel:hover{background:#dfe3e6}.confirm-delete:hover{background:#a93226}' +
            '.dark .confirm-box{background:#1e1e1e;color:#eee;box-shadow:0 12px 40px rgba(102,126,234,.35)}' +
            '.dark .confirm-title{color:#aaa}.dark .confirm-cancel{background:#333;color:#eee}' +
            '.dark .confirm-cancel:hover{background:#3d3d3d}' +
            '.book-item.deleting{transition:opacity .3s,transform .3s;opacity:0;transform:scale(.97)}';
        document.head.appendChild(style);
    }

    function confirmDelete(title, onConfirm) {
        var overlay = document.createElement('div');
        overlay.className = 'confirm-overlay';
        overlay.innerHTML = '<div class="confirm-box" role="dialog" aria-modal="true">' +
            '<p class="confirm-question"></p><p class="confirm-title"></p><div class="confirm-buttons">' +
            '<button type="button" class="confirm-cancel"></button><button type="button" class="confirm-delete"></button>' +
            '</div></div>';
        overlay.querySelector('.confirm-question').textContent = deleteTexts.question;
        overlay.querySelector('.confirm-title').textContent = title;
        var cancel = overlay.querySelector('.confirm-cancel');
        var confirm = overlay.querySelector('.confirm-delete');
        cancel.textContent = deleteTexts.cancel;
        confirm.textContent = deleteTexts.confirm;

        function close() {
            document.removeEventListener('keydown', onKey);
            overlay.classList.remove('shown');
            setTimeout(function () { overlay.remove(); }, 150);
        }
        function onKey(e) {
            if (e.key === 'Escape') close();
        }
        cancel.addEventListener('click', close);
        confirm.addEventListener('click', function () { close(); onConfirm(); });
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });
        document.addEventListener('keydown', onKey);

        document.body.appendChild(overlay);
        requestAnimationFrame(function () { overlay.classList.add('shown'); });
        cancel.focus();
    }

    function deleteBook(button) {
        fetch('/delete-book?id=' + encodeURIComponent(button.getAttribute('data-id')), { method: 'POST', credentials: 'same-origin' })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                if (!result.ok) {
                    showMessage(result.message, true);
                    return;
                }
                var item = button.closest('.book-item');
                if (item) {
                    item.classList.add('deleting');
                    setTimeout(function () { item.remove(); }, 300);
                }
                showMessage(result.message);
            })
            .catch(function () { showMessage(deleteTexts.failed, true); });
    }

    function setupDelete() {
        addDialogStyles();
        // Book lists grow while scrolling, so the clicks are caught on the page, before the handlers of the book card
        document.addEventListener('click', function (e) {
            var button = e.target.closest ? e.target.closest('.delete-book') : null;
            if (!button) return;
            e.preventDefault();
            e.stopPropagation();
            try {
                confirmDelete(button.getAttribute('data-title'), function () { deleteBook(button); });
            } catch (error) {
                // The browser can't build the dialog: its own one asks then
                if (window.confirm(deleteTexts.question + ' ' + button.getAttribute('data-title'))) deleteBook(button);
            }
        }, true);
    }

    function start() {
        setup();
        setupDelete();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})();
