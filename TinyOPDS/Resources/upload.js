/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Book uploads: double click (double tap) on the header logo opens the file dialog.
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
        input.accept = '.fb2,.epub,.zip';
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

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup);
    else setup();
})();
