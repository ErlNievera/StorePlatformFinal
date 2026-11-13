// Order details: polls the order every 2 seconds and redraws the saga timeline until the order
// is Confirmed or Cancelled. Compensation steps are drawn in red.
(function () {
  "use strict";

  var HAPPY_PATH = ["Pending", "StockReserved", "PaymentCharged", "Confirmed"];
  var TITLES = {
    Pending: "Order placed",
    StockReserved: "Stock reserved",
    PaymentCharged: "Payment charged",
    Confirmed: "Order confirmed",
    Cancelling: "Something went wrong",
    Cancelled: "Order cancelled"
  };
  var ICONS = {
    Pending: "bi-receipt",
    StockReserved: "bi-box-seam",
    PaymentCharged: "bi-credit-card",
    Confirmed: "bi-check-lg",
    Cancelling: "bi-exclamation-lg",
    Cancelled: "bi-x-lg"
  };

  function formatTime(iso) {
    var date = new Date(iso);
    return isNaN(date.getTime()) ? "" : date.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "medium" });
  }

  function step(className, icon, title, detail, time) {
    var li = document.createElement("li");
    li.className = "timeline-step " + className;
    var dot = document.createElement("span");
    dot.className = "timeline-dot";
    dot.setAttribute("aria-hidden", "true");
    dot.innerHTML = '<i class="bi ' + icon + '"></i>';
    var titleEl = document.createElement("div");
    titleEl.className = "timeline-title";
    titleEl.textContent = title;
    li.appendChild(dot);
    li.appendChild(titleEl);
    if (detail) {
      var detailEl = document.createElement("div");
      detailEl.className = "small";
      detailEl.textContent = detail;
      li.appendChild(detailEl);
    }
    if (time) {
      var timeEl = document.createElement("div");
      timeEl.className = "timeline-time";
      timeEl.textContent = time;
      li.appendChild(timeEl);
    }
    return li;
  }

  function render(list, data, previousCount) {
    var history = data.history || [];
    list.innerHTML = "";

    history.forEach(function (entry, index) {
      var failed = entry.status === "Cancelling" || entry.status === "Cancelled";
      var className = entry.isCompensation ? "compensation done" : failed ? "failed" : "done";
      var isLast = index === history.length - 1;
      if (isLast && !data.final) { className += " current"; }
      if (index >= previousCount && previousCount > 0) { className += " just-added"; }

      var title = entry.isCompensation ? "Compensation: " + (entry.reason || "step completed") : TITLES[entry.status] || entry.status;
      var detail = entry.isCompensation ? null : (entry.reason && entry.reason !== title ? entry.reason : null);
      var icon = entry.isCompensation ? "bi-arrow-counterclockwise" : ICONS[entry.status] || "bi-dot";
      list.appendChild(step(className, icon, title, detail, formatTime(entry.occurredAt)));
    });

    if (!data.final) {
      var reached = history.map(function (h) { return h.status; });
      var compensating = reached.indexOf("Cancelling") >= 0;
      var upcoming = compensating ? ["Cancelled"] : HAPPY_PATH.filter(function (s) { return reached.indexOf(s) < 0; });
      upcoming.forEach(function (status) {
        list.appendChild(step("upcoming", ICONS[status], TITLES[status], compensating ? "Undoing the earlier steps…" : "Waiting…", null));
      });
    }
  }

  function updateBadge(data) {
    var badge = document.getElementById("orderStatusBadge");
    if (badge) {
      badge.className = "status-pill status-" + data.status;
      badge.textContent = data.status === "StockReserved" ? "Stock reserved" : data.status === "PaymentCharged" ? "Payment charged" : data.status;
    }
    var reason = document.getElementById("cancellationReason");
    if (reason) {
      reason.hidden = !data.cancellationReason;
      reason.querySelector("span").textContent = data.cancellationReason || "";
    }
    var live = document.getElementById("timelineLive");
    if (live) {
      live.textContent = data.final ? "Order " + data.status.toLowerCase() + "." : "Status: " + (TITLES[data.status] || data.status);
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    var container = document.getElementById("sagaTimeline");
    if (!container) { return; }

    var list = container.querySelector(".timeline");
    var url = container.getAttribute("data-status-url");
    var final = container.getAttribute("data-final") === "true";
    var seen = parseInt(container.getAttribute("data-count") || "0", 10);
    var lastStatus = container.getAttribute("data-status");
    var failures = 0;

    function poll() {
      fetch(url, { headers: { "Accept": "application/json" }, credentials: "same-origin" })
        .then(function (response) {
          if (response.status === 401) { window.location.reload(); return null; }
          if (!response.ok) { throw new Error("HTTP " + response.status); }
          return response.json();
        })
        .then(function (data) {
          if (!data) { return; }
          failures = 0;
          render(list, data, seen);
          updateBadge(data);
          seen = (data.history || []).length;
          if (data.status !== lastStatus && data.final && window.acToast) {
            window.acToast(data.status === "Confirmed" ? "success" : "danger",
              data.status === "Confirmed" ? "Your order is confirmed!" : "Your order was cancelled: " + (data.cancellationReason || "see the timeline."));
          }
          lastStatus = data.status;
          if (!data.final) { setTimeout(poll, 2000); }
          else {
            container.setAttribute("data-final", "true");
            var note = document.getElementById("pollingNote");
            if (note) { note.remove(); }
          }
        })
        .catch(function () {
          failures += 1;
          if (failures === 3 && window.acToast) { window.acToast("warning", "Having trouble reaching the store; still trying…"); }
          setTimeout(poll, Math.min(10000, 2000 * failures));
        });
    }

    if (!final) { setTimeout(poll, 2000); }
  });
})();
