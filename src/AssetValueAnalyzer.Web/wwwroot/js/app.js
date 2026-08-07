document.addEventListener("DOMContentLoaded", () => {
  const infoTooltips = [...document.querySelectorAll("[data-info-tooltip]")].map((container) => {
    const trigger = container.querySelector("button");
    const content = container.querySelector("[data-info-tooltip-content]");

    document.body.append(content);
    return { container, trigger, content };
  });

  const hideInfoTooltips = () => {
    infoTooltips.forEach(({ content }) => {
      content.hidden = true;
    });
  };

  infoTooltips.forEach(({ container, trigger, content }) => {
    const show = () => {
      hideInfoTooltips();
      content.style.visibility = "hidden";
      content.hidden = false;

      const margin = 12;
      const gap = 8;
      const triggerRect = trigger.getBoundingClientRect();
      const contentRect = content.getBoundingClientRect();
      const left = Math.min(
        Math.max(triggerRect.left + (triggerRect.width / 2) - (contentRect.width / 2), margin),
        window.innerWidth - contentRect.width - margin,
      );
      const top = triggerRect.bottom + gap;

      content.style.left = `${left}px`;
      content.style.top = `${top}px`;
      content.style.visibility = "visible";
    };

    const hide = () => {
      content.hidden = true;
    };

    container.addEventListener("pointerenter", show);
    container.addEventListener("pointerleave", hide);
    trigger.addEventListener("focus", show);
    trigger.addEventListener("blur", hide);
  });

  window.addEventListener("scroll", hideInfoTooltips, true);
  window.addEventListener("resize", hideInfoTooltips);

  const monthNames = [
    "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
    "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık",
  ];

  const formatMonth = (value) => {
    if (!value) {
      return "—";
    }

    const [year, month] = value.split("-").map(Number);
    return `${monthNames[month - 1]} ${year}`;
  };

  const countMonthsInclusive = (startValue, endValue) => {
    const [startYear, startMonth] = startValue.split("-").map(Number);
    const [endYear, endMonth] = endValue.split("-").map(Number);
    return ((endYear - startYear) * 12) + endMonth - startMonth + 1;
  };

  const monthPickers = [...document.querySelectorAll("[data-month-picker]")];

  const closeMonthPickers = (except = null) => {
    monthPickers.forEach((picker) => {
      if (picker === except) {
        return;
      }

      const panel = picker.querySelector("[data-month-picker-panel]");
      const toggle = picker.querySelector("[data-month-picker-toggle]");
      panel.hidden = true;
      toggle.setAttribute("aria-expanded", "false");
    });
  };

  const positionMonthPanel = (toggle, panel) => {
    const margin = 16;
    const gap = 8;
    const toggleRect = toggle.getBoundingClientRect();
    const panelWidth = Math.min(Math.max(toggleRect.width, 288), window.innerWidth - margin * 2);

    panel.style.width = `${panelWidth}px`;
    panel.style.left = `${Math.min(Math.max(toggleRect.left, margin), window.innerWidth - panelWidth - margin)}px`;
    panel.style.visibility = "hidden";
    panel.hidden = false;

    const panelHeight = panel.getBoundingClientRect().height;
    const spaceBelow = window.innerHeight - toggleRect.bottom - margin;
    const top = spaceBelow >= panelHeight
      ? toggleRect.bottom + gap
      : Math.max(margin, toggleRect.top - panelHeight - gap);

    panel.style.top = `${top}px`;
    panel.style.visibility = "visible";
  };

  monthPickers.forEach((picker) => {
    const toggle = picker.querySelector("[data-month-picker-toggle]");
    const panel = picker.querySelector("[data-month-picker-panel]");
    const input = picker.querySelector("[data-month-picker-input]");
    const display = picker.querySelector("[data-month-picker-display]");
    const yearLabel = picker.querySelector("[data-month-year]");
    const previousYear = picker.querySelector("[data-month-year-previous]");
    const nextYear = picker.querySelector("[data-month-year-next]");
    const grid = picker.querySelector("[data-month-grid]");
    const minMonth = picker.dataset.minMonth;
    const maxMonth = picker.dataset.maxMonth;
    const minYear = Number(minMonth.slice(0, 4));
    const maxYear = Number(maxMonth.slice(0, 4));
    let viewYear = Number(input.value.slice(0, 4));

    const renderMonths = () => {
      yearLabel.textContent = String(viewYear);
      previousYear.disabled = viewYear <= minYear;
      nextYear.disabled = viewYear >= maxYear;
      grid.replaceChildren();

      monthNames.forEach((monthName, monthIndex) => {
        const value = `${viewYear}-${String(monthIndex + 1).padStart(2, "0")}`;
        const isDisabled = value < minMonth || value > maxMonth;
        const isSelected = value === input.value;
        const button = document.createElement("button");

        button.type = "button";
        button.textContent = monthName.slice(0, 3);
        button.disabled = isDisabled;
        button.setAttribute("aria-label", `${monthName} ${viewYear}`);
        button.setAttribute("aria-pressed", String(isSelected));
        button.className = "rounded-lg border px-2 py-2 text-sm transition";
        button.classList.add(
          isSelected ? "border-accent-300" : "border-line-700",
          isSelected ? "bg-accent-300" : "bg-canvas-950/70",
          isSelected ? "text-canvas-950" : "text-slate-200",
        );

        if (isDisabled) {
          button.classList.add("cursor-not-allowed", "opacity-30");
        } else {
          button.classList.add("hover:border-accent-300", "hover:text-accent-300");
        }

        button.addEventListener("click", () => {
          input.value = value;
          display.textContent = formatMonth(value);
          panel.hidden = true;
          toggle.setAttribute("aria-expanded", "false");
          input.dispatchEvent(new Event("change", { bubbles: true }));
          renderMonths();
          toggle.focus();
        });

        grid.append(button);
      });
    };

    toggle.addEventListener("click", () => {
      const willOpen = panel.hidden;
      closeMonthPickers(picker);

      if (!willOpen) {
        panel.hidden = true;
        toggle.setAttribute("aria-expanded", "false");
        return;
      }

      viewYear = Number(input.value.slice(0, 4));
      renderMonths();
      positionMonthPanel(toggle, panel);
      toggle.setAttribute("aria-expanded", "true");
    });

    previousYear.addEventListener("click", () => {
      viewYear = Math.max(minYear, viewYear - 1);
      renderMonths();
      positionMonthPanel(toggle, panel);
    });

    nextYear.addEventListener("click", () => {
      viewYear = Math.min(maxYear, viewYear + 1);
      renderMonths();
      positionMonthPanel(toggle, panel);
    });

    renderMonths();
  });

  document.addEventListener("click", (event) => {
    if (!monthPickers.some((picker) => picker.contains(event.target))) {
      closeMonthPickers();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      closeMonthPickers();
    }
  });

  window.addEventListener("resize", () => closeMonthPickers());

  const dateRange = document.querySelector("[data-date-range]");

  if (dateRange) {
    const toggle = dateRange.querySelector("[data-date-range-toggle]");
    const panel = dateRange.querySelector("[data-date-range-panel]");
    const chevron = dateRange.querySelector("[data-date-range-chevron]");
    const apply = dateRange.querySelector("[data-date-range-apply]");
    const start = dateRange.querySelector("[data-date-range-start]");
    const end = dateRange.querySelector("[data-date-range-end]");
    const error = dateRange.querySelector("[data-date-range-error]");
    const reportPeriod = document.querySelector("[data-report-period]");

    const setDateRangeOpen = (isOpen) => {
      panel.hidden = !isOpen;
      toggle.setAttribute("aria-expanded", String(isOpen));
      chevron.classList.toggle("rotate-180", isOpen);

      if (!isOpen) {
        closeMonthPickers();
      }
    };

    const validateRange = () => {
      const isValid = start.value <= end.value;
      error.hidden = isValid;
      return isValid;
    };

    start.addEventListener("change", validateRange);
    end.addEventListener("change", validateRange);
    toggle.addEventListener("click", () => setDateRangeOpen(panel.hidden));

    dateRange.addEventListener("click", (event) => {
      if (!event.target.closest("[data-month-picker]")) {
        closeMonthPickers();
      }

      event.stopPropagation();
    });

    apply.addEventListener("click", () => {
      if (!validateRange()) {
        return;
      }

      reportPeriod.textContent = `${formatMonth(start.value)} – ${formatMonth(end.value)}`;
      setDateRangeOpen(false);
    });

    document.addEventListener("click", () => setDateRangeOpen(false));
  }

  const reportPagination = document.querySelector("[data-report-pagination]");

  if (reportPagination) {
    const rows = [...document.querySelectorAll("[data-report-row]")];
    const pageSize = Number(reportPagination.dataset.pageSize ?? 10);
    const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
    const summary = reportPagination.querySelector("[data-pagination-summary]");
    const previous = reportPagination.querySelector("[data-page-previous]");
    const next = reportPagination.querySelector("[data-page-next]");
    const pageButtons = [...reportPagination.querySelectorAll("[data-page-number]")];
    let currentPage = 1;

    const renderPage = () => {
      const firstIndex = (currentPage - 1) * pageSize;
      const lastIndex = Math.min(firstIndex + pageSize, rows.length);

      rows.forEach((row, index) => {
        row.hidden = index < firstIndex || index >= lastIndex;
      });

      summary.textContent = `${firstIndex + 1}–${lastIndex} / ${rows.length} ay`;
      previous.disabled = currentPage === 1;
      next.disabled = currentPage === pageCount;

      pageButtons.forEach((button) => {
        const isCurrent = Number(button.dataset.pageNumber) === currentPage;
        button.setAttribute("aria-current", isCurrent ? "page" : "false");
        button.classList.toggle("bg-brand-500", isCurrent);
        button.classList.toggle("text-white", isCurrent);
        button.classList.toggle("border-brand-400", isCurrent);
      });
    };

    previous.addEventListener("click", () => {
      currentPage = Math.max(1, currentPage - 1);
      renderPage();
    });

    next.addEventListener("click", () => {
      currentPage = Math.min(pageCount, currentPage + 1);
      renderPage();
    });

    pageButtons.forEach((button) => {
      button.addEventListener("click", () => {
        currentPage = Number(button.dataset.pageNumber);
        renderPage();
      });
    });

    renderPage();
  }

  const wizard = document.querySelector("[data-report-wizard]");

  if (!wizard) {
    return;
  }

  const state = {
    step: 1,
    assetFile: null,
    indexFile: null,
  };

  const panels = [...wizard.querySelectorAll("[data-step-panel]")];
  const markers = [...wizard.querySelectorAll("[data-step-marker]")];
  const connectors = [...wizard.querySelectorAll("[data-step-connector]")];
  const fileInputs = [...wizard.querySelectorAll("[data-file-input]")];
  const continueButton = wizard.querySelector("[data-step-one-continue]");
  const rangeError = wizard.querySelector("[data-month-range-error]");
  const startMonthInput = wizard.querySelector("#startMonth");
  const endMonthInput = wizard.querySelector("#endMonth");

  const validateWizardRange = () => {
    const isValid = startMonthInput.value <= endMonthInput.value;
    rangeError.hidden = isValid;
    return isValid;
  };

  startMonthInput.addEventListener("change", validateWizardRange);
  endMonthInput.addEventListener("change", validateWizardRange);

  const setFileState = (input) => {
    const kind = input.dataset.fileInput;
    const file = input.files?.[0] ?? null;
    state[kind] = file;

    const row = wizard.querySelector(`[data-file-row="${kind}"]`);
    const name = row.querySelector("[data-file-name]");
    const status = row.querySelector("[data-file-status]");

    if (file) {
      name.textContent = file.name;
      name.classList.remove("text-slate-400");
      name.classList.add("text-white");
      status.hidden = false;
      status.classList.remove("hidden");
      status.textContent = "Seçildi";
      status.classList.remove("text-slate-400", "border-slate-600");
      status.classList.add("text-positive-400", "border-positive-400/45");
    } else {
      name.textContent = "Henüz dosya seçilmedi";
      name.classList.add("text-slate-400");
      name.classList.remove("text-white");
      status.hidden = true;
      status.classList.add("hidden");
      status.classList.add("text-slate-400", "border-slate-600");
      status.classList.remove("text-positive-400", "border-positive-400/45");
    }

    continueButton.disabled = !(state.assetFile && state.indexFile);
  };

  const renderStep = () => {
    panels.forEach((panel) => {
      panel.hidden = Number(panel.dataset.stepPanel) !== state.step;
    });

    markers.forEach((marker) => {
      const markerStep = Number(marker.dataset.stepMarker);
      const circle = marker.querySelector("[data-step-circle]");
      const label = marker.querySelector("[data-step-label]");
      const isActive = markerStep === state.step;
      const isComplete = markerStep < state.step;

      circle.className = "flex h-10 w-10 items-center justify-center rounded-full border font-semibold";
      circle.classList.add(
        isActive || isComplete ? "border-accent-300" : "border-slate-600",
        isActive ? "bg-accent-300" : isComplete ? "bg-accent-300/10" : "bg-slate-700/70",
        isActive ? "text-canvas-950" : isComplete ? "text-accent-300" : "text-slate-200",
      );
      label.classList.toggle("text-white", markerStep <= state.step);
      label.classList.toggle("text-slate-400", markerStep > state.step);
    });

    connectors.forEach((connector) => {
      const isComplete = Number(connector.dataset.stepConnector) < state.step;
      connector.classList.toggle("bg-accent-300", isComplete);
      connector.classList.toggle("bg-slate-600/70", !isComplete);
    });
  };

  const updateFileSummaries = () => {
    wizard.querySelectorAll("[data-summary-asset]").forEach((element) => {
      element.textContent = state.assetFile?.name ?? "—";
    });

    wizard.querySelectorAll("[data-summary-index]").forEach((element) => {
      element.textContent = state.indexFile?.name ?? "—";
    });
  };

  fileInputs.forEach((input) => {
    input.addEventListener("change", () => setFileState(input));
  });

  wizard.querySelectorAll("[data-go-step]").forEach((button) => {
    button.addEventListener("click", () => {
      const nextStep = Number(button.dataset.goStep);

      if (nextStep === 2 && !(state.assetFile && state.indexFile)) {
        return;
      }

      if (nextStep === 3 && !validateWizardRange()) {
        return;
      }

      state.step = nextStep;
      updateFileSummaries();

      if (nextStep === 3) {
        wizard.querySelector("[data-summary-period]").textContent =
          `${formatMonth(startMonthInput.value)} – ${formatMonth(endMonthInput.value)}`;
        wizard.querySelector("[data-summary-duration]").textContent =
          `${countMonthsInclusive(startMonthInput.value, endMonthInput.value)} ay`;
      }

      closeMonthPickers();
      renderStep();
      wizard.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  });

  renderStep();
});
