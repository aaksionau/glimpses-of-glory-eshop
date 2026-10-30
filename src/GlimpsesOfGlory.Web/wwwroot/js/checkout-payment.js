// Card payment on /checkout/payment (markup in Pages/Checkout/_PaymentForm.cshtml).
// Mounts the Stripe Payment Element and enables the Pay button once it's ready. While
// stripe.confirmPayment runs the button shows a spinner and "Processing…" with aria-busy, and
// goes back to normal if the payment fails. If Stripe.js or the Payment Element can't load, a
// message says so instead of leaving a blank area. Every error is put in the #payment-error
// live region, scrolled into view and focused so it's seen and announced.
(function () {
    'use strict';

    var form = document.getElementById('payment-form');
    if (!form) return;

    var submitButton = document.getElementById('submit-payment');
    var errorBox = document.getElementById('payment-error');

    function showError(message) {
        errorBox.textContent = message;
        errorBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
        errorBox.focus({ preventScroll: true });
    }

    function clearError() {
        errorBox.textContent = '';
    }

    function setProcessing(processing) {
        submitButton.disabled = processing;
        submitButton.setAttribute('aria-busy', processing ? 'true' : 'false');
        submitButton.querySelectorAll('[data-processing]').forEach(function (el) { el.hidden = !processing; });
        submitButton.querySelectorAll('[data-idle]').forEach(function (el) { el.hidden = processing; });
    }

    function formUnavailable() {
        submitButton.disabled = true;
        showError(form.dataset.loadError);
    }

    // Stripe.js comes from js.stripe.com in the script tag before this one; if that request
    // failed (blocked, offline) the Stripe global is missing.
    if (typeof window.Stripe !== 'function') {
        formUnavailable();
        return;
    }

    var stripe, elements, paymentElement;
    try {
        stripe = window.Stripe(form.dataset.publishableKey);
        elements = stripe.elements({ clientSecret: form.dataset.clientSecret });
        paymentElement = elements.create('payment');
        paymentElement.on('ready', function () { submitButton.disabled = false; });
        paymentElement.on('loaderror', formUnavailable);
        paymentElement.mount('#payment-element');
    } catch (e) {
        formUnavailable();
        return;
    }

    submitButton.addEventListener('click', async function () {
        if (submitButton.disabled) return;
        setProcessing(true);
        clearError();

        var result;
        try {
            result = await stripe.confirmPayment({ elements: elements, redirect: 'if_required' });
        } catch (e) {
            result = { error: {} };
        }

        if (result.error) {
            setProcessing(false);
            showError(result.error.message || form.dataset.paymentError);
            return;
        }

        // Leaving the page: keep the processing state until the confirmation page loads.
        window.location.href = '/checkout/confirmation?payment_intent=' + encodeURIComponent(result.paymentIntent.id);
    });
})();
