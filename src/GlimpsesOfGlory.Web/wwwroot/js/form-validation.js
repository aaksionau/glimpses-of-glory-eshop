// Inline validation for forms marked data-validate, built on the browser's own constraint
// validation (required, type=email, ...). Messages come from the fields' data-val-required /
// data-val-email attributes (the server's wording) and are shown in the field's
// [data-valmsg-for] span, the same place the server renders them. An invalid field gets
// aria-invalid and an aria-describedby pointing at "<field id>-error", matching the ids the
// server-side tag helpers (FieldValidationAriaTagHelper, ValidationMessageIdTagHelper) render.
// Disabled fields, including those in a disabled fieldset, are skipped (willValidate is false).
// Without this script the browser's native validation still applies.
(function () {
    'use strict';

    function validatable(el) {
        return el.name && el.willValidate && el.type !== 'radio' && el.type !== 'checkbox';
    }

    function messageFor(el) {
        var v = el.validity;
        if (v.valueMissing) return el.dataset.valRequired || el.validationMessage;
        if (v.typeMismatch && el.type === 'email') return el.dataset.valEmail || el.validationMessage;
        return el.validationMessage;
    }

    function messageSpan(form, el) {
        return form.querySelector('[data-valmsg-for="' + CSS.escape(el.name) + '"]');
    }

    function describedBy(el, id, linked) {
        var ids = (el.getAttribute('aria-describedby') || '').split(/\s+/).filter(function (x) {
            return x && x !== id;
        });
        if (linked) ids.push(id);
        if (ids.length) el.setAttribute('aria-describedby', ids.join(' '));
        else el.removeAttribute('aria-describedby');
    }

    // Shows or clears the field's message; returns whether the field is valid.
    function check(form, el) {
        var valid = el.checkValidity();
        var span = messageSpan(form, el);
        var id = el.id + '-error';

        el.classList.toggle('input-validation-error', !valid);
        if (valid) el.removeAttribute('aria-invalid');
        else el.setAttribute('aria-invalid', 'true');
        describedBy(el, id, !valid && !!span);

        if (span) {
            span.id = id;
            span.textContent = valid ? '' : messageFor(el);
            span.classList.toggle('field-validation-error', !valid);
            span.classList.toggle('field-validation-valid', valid);
        }
        return valid;
    }

    function showSummary(summary, invalid) {
        if (!summary) return;
        summary.replaceChildren();
        if (!invalid.length) return;
        var list = document.createElement('ul');
        invalid.forEach(function (el) {
            var item = document.createElement('li');
            item.textContent = messageFor(el);
            list.appendChild(item);
        });
        summary.appendChild(list);
    }

    function setUp(form) {
        // The script renders its own messages, so the browser's bubbles are turned off here
        // (not in the markup, so they still work if the script doesn't run).
        form.noValidate = true;
        var summary = form.querySelector('[data-validation-summary]');
        var edited = new WeakSet();

        form.addEventListener('input', function (e) {
            edited.add(e.target);
            // Clear a shown error as soon as the field is fixed.
            if (e.target.getAttribute('aria-invalid') === 'true') check(form, e.target);
        });

        // Check on blur once the shopper has typed in the field, or if it's already flagged,
        // so tabbing through an untouched form doesn't light it up.
        form.addEventListener('focusout', function (e) {
            var el = e.target;
            if (!validatable(el)) return;
            if (edited.has(el) || el.getAttribute('aria-invalid') === 'true') check(form, el);
        });

        form.addEventListener('submit', function (e) {
            var invalid = Array.from(form.elements).filter(validatable).filter(function (el) { return !check(form, el); });
            showSummary(summary, invalid);
            if (invalid.length) {
                e.preventDefault();
                invalid[0].focus();
            }
        });

        // Back from a failed post: start on the first field the server flagged.
        var flagged = form.querySelector('[aria-invalid="true"]');
        if (flagged) flagged.focus();
    }

    function init() {
        document.querySelectorAll('form[data-validate]').forEach(setUp);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
