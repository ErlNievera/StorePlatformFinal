// UST Angelicum College Store - shared page behaviour.
(function () {
  "use strict";

  var THEME_KEY = "ac-theme";
  var SPLASH_KEY = "ac-splash-seen";

  // ---- Theme toggle (the initial theme is applied inline in <head> to avoid a flash) ----
  function currentTheme() {
    return document.documentElement.getAttribute("data-bs-theme") === "dark" ? "dark" : "light";
  }

  function applyTheme(theme) {
    document.documentElement.setAttribute("data-bs-theme", theme);
    var button = document.getElementById("themeToggle");
    if (button) {
      var dark = theme === "dark";
      button.setAttribute("aria-pressed", dark ? "true" : "false");
      button.setAttribute("aria-label", dark ? "Switch to light mode" : "Switch to dark mode");
      button.innerHTML = dark ? '<i class="bi bi-sun" aria-hidden="true"></i>' : '<i class="bi bi-moon-stars" aria-hidden="true"></i>';
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    applyTheme(currentTheme());
    var toggle = document.getElementById("themeToggle");
    if (toggle) {
      toggle.addEventListener("click", function () {
        var next = currentTheme() === "dark" ? "light" : "dark";
        try { localStorage.setItem(THEME_KEY, next); } catch (e) { /* storage blocked: theme still switches for this page */ }
        applyTheme(next);
      });
    }

    // ---- Splash: once per browser session ----
    var splash = document.getElementById("splash");
    if (splash) {
      var seen = false;
      try { seen = sessionStorage.getItem(SPLASH_KEY) === "1"; } catch (e) { seen = true; }
      if (seen) {
        splash.remove();
      } else {
        try { sessionStorage.setItem(SPLASH_KEY, "1"); } catch (e) { /* ignore */ }
        window.addEventListener("load", function () {
          setTimeout(function () {
            splash.classList.add("is-hidden");
            setTimeout(function () { splash.remove(); }, 600);
          }, 700);
        });
      }
    }

    // ---- Staggered cards: give each child its index ----
    document.querySelectorAll(".stagger").forEach(function (container) {
      Array.prototype.forEach.call(container.children, function (child, index) {
        child.style.setProperty("--i", Math.min(index, 12));
      });
    });

    // ---- Toasts rendered by the server ----
    document.querySelectorAll(".toast[data-autoshow]").forEach(function (element) {
      if (window.bootstrap) {
        new bootstrap.Toast(element, { delay: 5000 }).show();
      }
    });

    // ---- Button spinners and double-submit protection ----
    document.querySelectorAll("form").forEach(function (form) {
      form.addEventListener("submit", function (event) {
        if (form.dataset.submitting === "1") {
          event.preventDefault();
          return;
        }
        if (typeof form.checkValidity === "function" && !form.checkValidity()) {
          return;
        }
        form.dataset.submitting = "1";
        var button = event.submitter || form.querySelector("[type=submit]");
        if (button && !button.classList.contains("no-spinner")) {
          button.classList.add("is-loading");
          button.setAttribute("aria-busy", "true");
        }
      });
    });
  });

  // ---- Client-side toast helper used by other scripts ----
  window.acToast = function (kind, text) {
    var stack = document.getElementById("toastStack");
    if (!stack || !window.bootstrap) { return; }
    var element = document.createElement("div");
    element.className = "toast align-items-center text-bg-" + kind;
    element.setAttribute("role", kind === "danger" ? "alert" : "status");
    element.setAttribute("aria-live", kind === "danger" ? "assertive" : "polite");
    element.setAttribute("aria-atomic", "true");
    element.innerHTML = '<div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
    element.querySelector(".toast-body").textContent = text;
    stack.appendChild(element);
    new bootstrap.Toast(element, { delay: 5000 }).show();
    element.addEventListener("hidden.bs.toast", function () { element.remove(); });
  };
})();
