document.addEventListener("DOMContentLoaded", () => {
  const connectExchangeRateUpdates = () => {
    if (!document.querySelector("[data-exchange-rate-card]") || !window.signalR) {
      return;
    }

    let isPageUnloading = false;
    let retryTimeoutId = null;
    let refreshPromise = null;
    let startPromise = null;
    const cardRefreshIntervalMilliseconds = 30_000;

    const refreshExchangeRateCard = async () => {
      if (refreshPromise) {
        await refreshPromise;
        return;
      }

      refreshPromise = (async () => {
        const currentCard = document.querySelector("[data-exchange-rate-card]");

        if (!currentCard) {
          return;
        }

        try {
          const response = await fetch(currentCard.dataset.refreshUrl, {
            headers: { Accept: "text/html" },
            cache: "no-store",
          });

          if (!response.ok) {
            throw new Error(`Kur kartı yenilenemedi (${response.status}).`);
          }

          const template = document.createElement("template");
          template.innerHTML = (await response.text()).trim();
          const updatedCard = template.content.firstElementChild;

          if (!updatedCard?.matches("[data-exchange-rate-card]")) {
            throw new Error("Kur kartı cevabı beklenen HTML yapısında değil.");
          }

          currentCard.replaceWith(updatedCard);
        } catch (error) {
          console.warn("Kur kartı canlı olarak yenilenemedi.", error);
        }
      })();

      try {
        await refreshPromise;
      } finally {
        refreshPromise = null;
      }
    };

    const connection = new window.signalR.HubConnectionBuilder()
      .withUrl("/hubs/exchange-rates")
      .withAutomaticReconnect()
      .configureLogging(window.signalR.LogLevel.Warning)
      .build();

    connection.on("exchangeRatesSynchronized", () => {
      void refreshExchangeRateCard();
    });

    const clearConnectionRetry = () => {
      if (retryTimeoutId !== null) {
        window.clearTimeout(retryTimeoutId);
        retryTimeoutId = null;
      }
    };

    const scheduleConnectionStart = () => {
      if (isPageUnloading || retryTimeoutId !== null) {
        return;
      }

      retryTimeoutId = window.setTimeout(() => {
        retryTimeoutId = null;
        void startConnection();
      }, 5000);
    };

    const startConnection = async () => {
      const disconnectedState = window.signalR.HubConnectionState?.Disconnected ?? "Disconnected";

      if (isPageUnloading || startPromise || connection.state !== disconnectedState) {
        return;
      }

      clearConnectionRetry();

      try {
        startPromise = connection.start();
        await startPromise;
        void refreshExchangeRateCard();
      } catch (error) {
        if (!isPageUnloading) {
          console.warn("Kur güncelleme bağlantısı kurulamadı; tekrar denenecek.", error);
          scheduleConnectionStart();
        }
      } finally {
        startPromise = null;
      }
    };

    const catchUpExchangeRateCard = () => {
      if (document.visibilityState === "hidden") {
        return;
      }

      void refreshExchangeRateCard();
      void startConnection();
    };

    connection.onreconnected(() => {
      void refreshExchangeRateCard();
    });

    connection.onclose(() => {
      scheduleConnectionStart();
    });

    document.addEventListener("visibilitychange", () => {
      if (document.visibilityState === "visible") {
        catchUpExchangeRateCard();
      }
    });

    window.addEventListener("focus", catchUpExchangeRateCard);
    window.addEventListener("pageshow", (event) => {
      if (event.persisted) {
        isPageUnloading = false;
        catchUpExchangeRateCard();
      }
    });

    const refreshIntervalId = window.setInterval(() => {
      if (document.visibilityState === "visible") {
        void refreshExchangeRateCard();
      }
    }, cardRefreshIntervalMilliseconds);

    window.addEventListener("beforeunload", () => {
      isPageUnloading = true;
      clearConnectionRetry();
      window.clearInterval(refreshIntervalId);

      void connection.stop();
    }, { once: true });

    void startConnection();
  };

  connectExchangeRateUpdates();

  const wizardStorageKeys = {
    step: "AssetValueAnalyzer.ReportWizard.Step",
    startMonth: "AssetValueAnalyzer.ReportWizard.StartMonth",
    endMonth: "AssetValueAnalyzer.ReportWizard.EndMonth",
  };

  const clearWizardStorage = () => {
    try {
      Object.values(wizardStorageKeys).forEach((key) => window.sessionStorage.removeItem(key));
    } catch {
      // Session storage kullanılamıyorsa temizlenecek kalıcı UI durumu yoktur.
    }
  };

  if (document.querySelector('[data-reset-report-wizard="true"]')) {
    clearWizardStorage();
  }

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

  const getLastCompletedMonth = () => {
    const now = new Date();
    const previousMonth = new Date(now.getFullYear(), now.getMonth() - 1, 1);

    return `${previousMonth.getFullYear()}-${String(previousMonth.getMonth() + 1).padStart(2, "0")}`;
  };

  const monthPickers = [...document.querySelectorAll("[data-month-picker]")];

  const updateMonthPickerAccessibleName = (picker) => {
    const label = picker.dataset.monthPickerLabel;
    const toggle = picker.querySelector("[data-month-picker-toggle]");
    const display = picker.querySelector("[data-month-picker-display]");

    if (label && toggle && display) {
      toggle.setAttribute("aria-label", `${label}: ${display.textContent.trim()}`);
    }
  };

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
    const panelWidth = Math.min(toggleRect.width, window.innerWidth - margin * 2);

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
    const clear = picker.querySelector("[data-month-picker-clear]");
    const grid = picker.querySelector("[data-month-grid]");
    const getBounds = () => {
      const minMonth = picker.dataset.minMonth;
      const configuredMaxMonth = picker.dataset.maxMonth;
      const maxMonth = picker.dataset.completedMonthsOnly === "true"
        ? [configuredMaxMonth, getLastCompletedMonth()].sort()[0]
        : configuredMaxMonth;

      return {
        minMonth,
        maxMonth,
        minYear: Number(minMonth.slice(0, 4)),
        maxYear: Number(maxMonth.slice(0, 4)),
      };
    };
    let viewYear = Number(input.value.slice(0, 4)) || getBounds().minYear;

    const renderMonths = () => {
      const { minMonth, maxMonth, minYear, maxYear } = getBounds();
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
          isSelected ? "bg-accent-300" : "bg-step-surface",
          isSelected ? "text-paper-100" : "text-step-ink",
        );

        if (isDisabled) {
          button.classList.add("cursor-not-allowed", "opacity-30");
        } else {
          button.classList.add("hover:border-accent-300", "hover:text-accent-300");
        }

        button.addEventListener("click", () => {
          input.value = value;
          display.textContent = formatMonth(value);
          display.classList.add("text-ink-900");
          display.classList.remove("text-slate-400");
          updateMonthPickerAccessibleName(picker);
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

      viewYear = Number(input.value.slice(0, 4)) || getBounds().minYear;
      renderMonths();
      positionMonthPanel(toggle, panel);
      toggle.setAttribute("aria-expanded", "true");
    });

    previousYear.addEventListener("click", () => {
      const { minYear } = getBounds();
      viewYear = Math.max(minYear, viewYear - 1);
      renderMonths();
      positionMonthPanel(toggle, panel);
    });

    nextYear.addEventListener("click", () => {
      const { maxYear } = getBounds();
      viewYear = Math.min(maxYear, viewYear + 1);
      renderMonths();
      positionMonthPanel(toggle, panel);
    });

    clear?.addEventListener("click", () => {
      input.value = "";
      display.textContent = "Seçilmedi";
      display.classList.add("text-slate-400");
      display.classList.remove("text-ink-900");
      updateMonthPickerAccessibleName(picker);
      panel.hidden = true;
      toggle.setAttribute("aria-expanded", "false");
      input.dispatchEvent(new Event("change", { bubbles: true }));
      toggle.focus();
    });

    updateMonthPickerAccessibleName(picker);
    renderMonths();
  });

  document.addEventListener("click", (event) => {
    if (!monthPickers.some((picker) => picker.contains(event.target))) {
      closeMonthPickers();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      hideInfoTooltips();
      closeMonthPickers();
    }
  });

  window.addEventListener("resize", () => closeMonthPickers());

  const dateRange = document.querySelector("[data-date-range]");

  if (dateRange) {
    const toggle = dateRange.querySelector("[data-date-range-toggle]");
    const panel = dateRange.querySelector("[data-date-range-panel]");
    const chevron = dateRange.querySelector("[data-date-range-chevron]");
    const form = dateRange.querySelector("[data-date-range-form]");
    const apply = dateRange.querySelector("[data-date-range-apply]");
    const cancel = dateRange.querySelector("[data-date-range-cancel]");
    const start = dateRange.querySelector("[data-date-range-start]");
    const end = dateRange.querySelector("[data-date-range-end]");
    const error = dateRange.querySelector("[data-date-range-error]");
    const antiforgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    let validationRequestId = 0;
    let isRangeValid = false;

    const positionDateRangePanel = () => {
      const margin = 16;
      const gap = 8;
      const toggleRect = toggle.getBoundingClientRect();
      const panelWidth = Math.min(380, window.innerWidth - margin * 2);

      panel.style.width = `${panelWidth}px`;
      panel.style.left = `${Math.min(Math.max(toggleRect.right - panelWidth, margin), window.innerWidth - panelWidth - margin)}px`;
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

    const setError = (message) => {
      error.textContent = message;
      error.hidden = message.length === 0;
    };

    const setApplyState = (isValid) => {
      isRangeValid = isValid;
      apply.disabled = !isValid;
    };

    const setDateRangeOpen = (isOpen) => {
      if (isOpen) {
        positionDateRangePanel();
      } else {
        panel.hidden = true;
      }

      toggle.setAttribute("aria-expanded", String(isOpen));
      chevron.classList.toggle("rotate-180", isOpen);

      if (!isOpen) {
        closeMonthPickers();
      }
    };

    const getLocalRangeError = () => {
      if (!start.value || !end.value) {
        return "Başlangıç ve bitiş ayını seçin.";
      }

      if (start.value === end.value) {
        return "Rapor dönemi en az iki farklı varlık ayını içermelidir.";
      }

      if (start.value > end.value) {
        return "Başlangıç ayı bitiş ayından sonra olamaz.";
      }

      return "";
    };

    const validateRange = async () => {
      const localError = getLocalRangeError();

      if (localError) {
        validationRequestId++;
        setApplyState(false);
        setError(localError);
        return false;
      }

      const requestId = ++validationRequestId;
      setApplyState(false);
      setError("");

      const body = new URLSearchParams({
        StartMonth: start.value,
        EndMonth: end.value,
      });

      if (antiforgeryToken) {
        body.set("__RequestVerificationToken", antiforgeryToken);
      }

      try {
        const response = await fetch(dateRange.dataset.rangeValidationUrl, {
          method: "POST",
          headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" },
          body,
        });
        const result = await response.json();

        if (requestId !== validationRequestId) {
          return false;
        }

        if (!response.ok || !result.isValid) {
          setApplyState(false);
          setError(result.errors?.[0]?.message ?? "Seçilen rapor dönemi doğrulanamadı.");
          return false;
        }

        setApplyState(true);
        setError("");
        return true;
      } catch {
        if (requestId !== validationRequestId) {
          return false;
        }

        setApplyState(false);
        setError("Rapor dönemi şu anda doğrulanamadı. Lütfen yeniden deneyin.");
        return false;
      }
    };

    start.addEventListener("change", () => void validateRange());
    end.addEventListener("change", () => void validateRange());
    toggle.addEventListener("click", () => {
      const willOpen = panel.hidden;
      setDateRangeOpen(willOpen);

      if (willOpen) {
        void validateRange();
      }
    });
    cancel.addEventListener("click", () => setDateRangeOpen(false));

    dateRange.addEventListener("click", (event) => {
      if (!event.target.closest("[data-month-picker]")) {
        closeMonthPickers();
      }

      event.stopPropagation();
    });

    form.addEventListener("submit", async (event) => {
      event.preventDefault();

      if (!isRangeValid && !(await validateRange())) {
        return;
      }

      apply.disabled = true;
      form.submit();
    });

    document.addEventListener("click", () => setDateRangeOpen(false));

    window.addEventListener("resize", () => {
      if (!panel.hidden) {
        positionDateRangePanel();
      }
    });

    if (dateRange.dataset.openOnError === "true") {
      setDateRangeOpen(true);
    }
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
        button.classList.toggle("text-paper-100", isCurrent);
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
    assetFileName: wizard.dataset.assetFileName || null,
    indexFileName: wizard.dataset.indexFileName || null,
    assetValidationState: wizard.dataset.assetReady === "true" ? "valid" : "idle",
    assetValidationRequestId: 0,
    indexValidationState: wizard.dataset.indexReady === "true" ? "valid" : "idle",
    indexValidationRequestId: 0,
    assetFirstMonth: wizard.dataset.assetFirstMonth || null,
    assetLastMonth: wizard.dataset.assetLastMonth || null,
    rangeValidationState: "idle",
    rangeValidationRequestId: 0,
    includedMonthCount: null,
    reportLocked: wizard.dataset.reportLocked === "true",
    fileUploadInProgress: false,
  };

  const readWizardStorage = (key) => {
    try {
      return window.sessionStorage.getItem(key);
    } catch {
      return null;
    }
  };

  const writeWizardStorage = (key, value) => {
    try {
      window.sessionStorage.setItem(key, value);
    } catch {
      // Tarayıcı depolaması kapalıysa akış yalnız mevcut sayfa ömründe çalışır.
    }
  };

  const panels = [...wizard.querySelectorAll("[data-step-panel]")];
  const markers = [...wizard.querySelectorAll("[data-step-marker]")];
  const connectors = [...wizard.querySelectorAll("[data-step-connector]")];
  const fileInputs = [...wizard.querySelectorAll("[data-file-input]")];
  const continueButton = wizard.querySelector("[data-step-one-continue]");
  const rangeContinueButton = wizard.querySelector("[data-step-two-continue]");
  const rangeError = wizard.querySelector("[data-month-range-error]");
  const startMonthInput = wizard.querySelector("#startMonth");
  const endMonthInput = wizard.querySelector("#endMonth");
  const reportCreationError = wizard.querySelector("[data-report-creation-error]");
  const antiforgeryToken = wizard.querySelector('input[name="__RequestVerificationToken"]')?.value;
  const hasValidatedFiles =
    state.assetValidationState === "valid" &&
    state.indexValidationState === "valid";
  const storedStep = Number(readWizardStorage(wizardStorageKeys.step));
  const requestedInitialStep = hasValidatedFiles && [2, 3].includes(storedStep)
    ? storedStep
    : 1;

  if (!hasValidatedFiles) {
    clearWizardStorage();
  }

  const restoreMonthSelection = (input, storageKey) => {
    const storedValue = readWizardStorage(storageKey);

    if (!storedValue ||
        !state.assetFirstMonth ||
        !state.assetLastMonth ||
        storedValue < state.assetFirstMonth ||
        storedValue > state.assetLastMonth) {
      return;
    }

    const picker = input.closest("[data-month-picker]");
    const display = picker?.querySelector("[data-month-picker-display]");
    input.value = storedValue;

    if (display) {
      display.textContent = formatMonth(storedValue);
      display.classList.add("text-ink-900");
      display.classList.remove("text-slate-400");
      updateMonthPickerAccessibleName(picker);
    }
  };

  restoreMonthSelection(startMonthInput, wizardStorageKeys.startMonth);
  restoreMonthSelection(endMonthInput, wizardStorageKeys.endMonth);
  state.step = requestedInitialStep === 3 ? 2 : requestedInitialStep;

  const getLocalRangeError = () => {
    const startMonth = startMonthInput.value || state.assetFirstMonth;
    const endMonth = endMonthInput.value || state.assetLastMonth;

    if (startMonth && endMonth && startMonth === endMonth) {
      return "Rapor dönemi en az iki farklı varlık ayını içermelidir.";
    }

    if (startMonth && endMonth && startMonth > endMonth) {
      return "Başlangıç ayı bitiş ayından sonra olamaz.";
    }

    return "";
  };

  const setRangeError = (message) => {
    rangeError.textContent = message;
    rangeError.hidden = message.length === 0;
  };

  const updateRangeContinueState = () => {
    rangeContinueButton.disabled = state.rangeValidationState !== "valid";
  };

  const validateWizardRange = async () => {
    const localError = getLocalRangeError();

    if (localError) {
      state.rangeValidationState = "invalid";
      state.includedMonthCount = null;
      setRangeError(localError);
      updateRangeContinueState();
      return false;
    }

    if (!(state.assetValidationState === "valid" && state.indexValidationState === "valid")) {
      state.rangeValidationState = "idle";
      state.includedMonthCount = null;
      setRangeError("");
      updateRangeContinueState();
      return false;
    }

    const requestId = ++state.rangeValidationRequestId;
    state.rangeValidationState = "validating";
    state.includedMonthCount = null;
    setRangeError("");
    updateRangeContinueState();

    const body = new URLSearchParams({
      StartMonth: startMonthInput.value,
      EndMonth: endMonthInput.value,
    });

    if (antiforgeryToken) {
      body.set("__RequestVerificationToken", antiforgeryToken);
    }

    try {
      const response = await fetch(wizard.dataset.rangeValidationUrl, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" },
        body,
      });
      const result = await response.json();

      if (requestId !== state.rangeValidationRequestId) {
        return false;
      }

      if (!response.ok || !result.isValid) {
        state.rangeValidationState = "invalid";
        state.includedMonthCount = null;
        setRangeError(result.errors?.[0]?.message ?? "Seçilen rapor dönemi doğrulanamadı.");
        updateRangeContinueState();
        return false;
      }

      state.rangeValidationState = "valid";
      state.includedMonthCount = result.includedMonthCount;
      setRangeError("");
      updateRangeContinueState();
      return true;
    } catch {
      if (requestId !== state.rangeValidationRequestId) {
        return false;
      }

      state.rangeValidationState = "invalid";
      state.includedMonthCount = null;
      setRangeError("Rapor dönemi şu anda doğrulanamadı. Lütfen yeniden deneyin.");
      updateRangeContinueState();
      return false;
    }
  };

  const resetWizardRange = () => {
    wizard.querySelectorAll("[data-month-picker]").forEach((picker) => {
      if (state.assetFirstMonth && state.assetLastMonth) {
        picker.dataset.minMonth = state.assetFirstMonth;
        picker.dataset.maxMonth = state.assetLastMonth;
      }

      const input = picker.querySelector("[data-month-picker-input]");
      const display = picker.querySelector("[data-month-picker-display]");
      input.value = "";
      display.textContent = "Seçilmedi";
      display.classList.add("text-slate-400");
      display.classList.remove("text-ink-900");
      updateMonthPickerAccessibleName(picker);
    });

    state.rangeValidationRequestId++;
    state.rangeValidationState = "idle";
    state.includedMonthCount = null;
    writeWizardStorage(wizardStorageKeys.startMonth, "");
    writeWizardStorage(wizardStorageKeys.endMonth, "");
    writeWizardStorage(wizardStorageKeys.step, "1");
    setRangeError("");
    updateRangeContinueState();
  };

  const handleRangeChange = () => {
    if (reportCreationError) {
      reportCreationError.hidden = true;
    }

    writeWizardStorage(wizardStorageKeys.startMonth, startMonthInput.value);
    writeWizardStorage(wizardStorageKeys.endMonth, endMonthInput.value);
    void validateWizardRange();
  };

  startMonthInput.addEventListener("change", handleRangeChange);
  endMonthInput.addEventListener("change", handleRangeChange);

  const updateContinueState = () => {
    continueButton.disabled = !(
      state.assetValidationState === "valid" &&
      state.indexValidationState === "valid"
    );
  };

  const setStatus = (status, text, tone) => {
    status.hidden = false;
    status.classList.remove("hidden");
    status.textContent = text;
    status.classList.remove(
      "text-slate-400",
      "border-slate-600",
      "text-positive-400",
      "border-positive-400/45",
      "text-negative-400",
      "border-negative-400/45",
      "border-step-border",
    );

    if (tone === "positive") {
      status.classList.add("text-positive-400", "border-positive-400/45");
    } else if (tone === "negative") {
      status.classList.add("text-negative-400", "border-negative-400/45");
    } else {
      status.classList.add("text-slate-400", "border-step-border");
    }
  };

  const setValidationMessage = (element, message, tone = "neutral") => {
    if (!element) {
      return;
    }

    element.textContent = message;
    element.classList.toggle("text-negative-400", tone === "negative");
    element.classList.toggle("text-positive-400", tone === "positive");
    element.classList.toggle("text-slate-400", tone === "neutral");
  };

  const setClearButtonVisibility = (row, visible) => {
    const button = row.querySelector("[data-file-clear]");

    if (!button) {
      return;
    }

    button.hidden = !visible;
    button.style.display = visible ? "" : "none";
    button.classList.toggle("hidden", !visible);

    if (visible) {
      button.disabled = state.fileUploadInProgress;
    }
  };

  const setUploadControlsDisabled = (disabled) => {
    state.fileUploadInProgress = disabled;
    wizard.setAttribute("aria-busy", String(disabled));

    fileInputs.forEach((input) => {
      input.disabled = disabled;
      const label = input.closest("label");
      label?.classList.toggle("cursor-not-allowed", disabled);
      label?.classList.toggle("opacity-30", disabled);
    });

    wizard.querySelectorAll("[data-file-clear]").forEach((button) => {
      button.disabled = disabled;
    });
  };

  const validateAssetFile = async (file, row, requestId) => {
    const status = row.querySelector("[data-file-status]");
    const validation = row.querySelector("[data-file-validation]");
    const formData = new FormData();
    formData.append("file", file);

    if (antiforgeryToken) {
      formData.append("__RequestVerificationToken", antiforgeryToken);
    }

    state.assetValidationState = "validating";
    setStatus(status, "Doğrulanıyor…", "neutral");
    setValidationMessage(validation, "Aylık Varlık Verisi dosyasının yapısı ve tutarları kontrol ediliyor.");
    updateContinueState();

    try {
      const response = await fetch("/imports/assets/validate", {
        method: "POST",
        body: formData,
      });
      const result = await response.json();

      if (requestId !== state.assetValidationRequestId) {
        return;
      }

      if (!response.ok || !result.isValid) {
        const message = result.errors?.[0]?.message ?? "Aylık Varlık Verisi dosyası doğrulanamadı.";
        state.assetValidationState = "invalid";
        setStatus(status, "Doğrulanamadı", "negative");
        setValidationMessage(validation, message, "negative");
        setClearButtonVisibility(row, false);
        updateContinueState();
        return;
      }

      state.assetValidationState = "valid";
      state.assetFirstMonth = result.firstMonth.slice(0, 7);
      state.assetLastMonth = result.lastMonth.slice(0, 7);
      resetWizardRange();
      setStatus(status, "Doğrulandı", "positive");
      setValidationMessage(
        validation,
        `${formatMonth(result.firstMonth.slice(0, 7))} – ${formatMonth(result.lastMonth.slice(0, 7))}`,
        "positive",
      );
      setClearButtonVisibility(row, true);
    } catch {
      if (requestId !== state.assetValidationRequestId) {
        return;
      }

      state.assetValidationState = "invalid";
      setStatus(status, "Doğrulanamadı", "negative");
      setValidationMessage(
        validation,
        "Aylık Varlık Verisi dosyası şu anda doğrulanamadı. Lütfen yeniden deneyin.",
        "negative",
      );
    }

    updateContinueState();
  };

  const validateIndexFile = async (file, row, requestId) => {
    const status = row.querySelector("[data-file-status]");
    const validation = row.querySelector("[data-file-validation]");
    const formData = new FormData();
    formData.append("file", file);

    if (antiforgeryToken) {
      formData.append("__RequestVerificationToken", antiforgeryToken);
    }

    state.indexValidationState = "validating";
    setStatus(status, "Doğrulanıyor…", "neutral");
    setValidationMessage(validation, "Yİ-ÜFE Endeks Verisi dosyasının yıl-ay matrisi ve değerleri kontrol ediliyor.");
    updateContinueState();

    try {
      const response = await fetch("/imports/indices/validate", {
        method: "POST",
        body: formData,
      });
      const result = await response.json();

      if (requestId !== state.indexValidationRequestId) {
        return;
      }

      if (!response.ok || !result.isValid) {
        const message = result.errors?.[0]?.message ?? "Yİ-ÜFE Endeks Verisi dosyası doğrulanamadı.";
        state.indexValidationState = "invalid";
        setStatus(status, "Doğrulanamadı", "negative");
        setValidationMessage(validation, message, "negative");
        setClearButtonVisibility(row, false);
        updateContinueState();
        return;
      }

      state.indexValidationState = "valid";
      setStatus(status, "Doğrulandı", "positive");
      setValidationMessage(
        validation,
        `${formatMonth(result.firstMonth.slice(0, 7))} – ${formatMonth(result.lastMonth.slice(0, 7))}`,
        "positive",
      );
      setClearButtonVisibility(row, true);

      if (state.step === 2) {
        void validateWizardRange();
      }
    } catch {
      if (requestId !== state.indexValidationRequestId) {
        return;
      }

      state.indexValidationState = "invalid";
      setStatus(status, "Doğrulanamadı", "negative");
      setValidationMessage(
        validation,
        "Yİ-ÜFE Endeks Verisi dosyası şu anda doğrulanamadı. Lütfen yeniden deneyin.",
        "negative",
      );
    }

    updateContinueState();
  };

  const setFileState = async (input) => {
    const kind = input.dataset.fileInput;
    const file = input.files?.[0] ?? null;
    state[kind] = file;

    const row = wizard.querySelector(`[data-file-row="${kind}"]`);
    const name = row.querySelector("[data-file-name]");
    const status = row.querySelector("[data-file-status]");
    const validation = row.querySelector("[data-file-validation]");

    if (file) {
      if (kind === "assetFile") {
        state.assetFileName = file.name;
      } else {
        state.indexFileName = file.name;
      }

      name.textContent = file.name;
      name.classList.remove("text-slate-400");
      name.classList.add("text-ink-900");

      if (kind === "assetFile") {
        state.assetFirstMonth = null;
        state.assetLastMonth = null;
        resetWizardRange();
        const requestId = ++state.assetValidationRequestId;
        await validateAssetFile(file, row, requestId);
      } else {
        const requestId = ++state.indexValidationRequestId;
        await validateIndexFile(file, row, requestId);
      }
    } else {
      name.textContent = "Henüz dosya seçilmedi";
      name.classList.add("text-slate-400");
      name.classList.remove("text-ink-900");
      status.hidden = true;
      status.classList.add("hidden");
      setClearButtonVisibility(row, false);

      if (kind === "assetFile") {
        state.assetFileName = null;
        state.assetValidationRequestId++;
        state.assetValidationState = "idle";
        state.assetFirstMonth = null;
        state.assetLastMonth = null;
        resetWizardRange();
        setValidationMessage(validation, "");
      } else {
        state.indexFileName = null;
        state.indexValidationRequestId++;
        state.indexValidationState = "idle";
        setValidationMessage(validation, "");
      }
    }

    updateContinueState();
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
        isActive || isComplete ? "border-accent-300" : "border-step-border",
        isActive ? "bg-accent-300" : isComplete ? "bg-icon-surface" : "bg-step-surface",
        isActive ? "text-paper-100" : isComplete ? "text-accent-300" : "text-step-ink",
      );
      label.classList.toggle("text-ink-900", markerStep <= state.step);
      label.classList.toggle("text-step-muted", markerStep > state.step);
    });

    connectors.forEach((connector) => {
      const isComplete = Number(connector.dataset.stepConnector) < state.step;
      connector.classList.toggle("bg-accent-300", isComplete);
      connector.classList.toggle("bg-line-600", !isComplete);
    });
  };

  const updateConfirmationSummary = () => {
    const selectedStart = startMonthInput.value;
    const selectedEnd = endMonthInput.value;
    const effectiveStart = selectedStart || state.assetFirstMonth;
    const effectiveEnd = selectedEnd || state.assetLastMonth;
    let automaticNote = "";

    if (!selectedStart && !selectedEnd) {
      automaticNote = "Rapor dönemi dosyanın ilk ve son ayına göre belirlendi.";
    } else if (!selectedStart) {
      automaticNote = "Başlangıç ayı dosyadan otomatik belirlendi.";
    } else if (!selectedEnd) {
      automaticNote = "Bitiş ayı dosyadan otomatik belirlendi.";
    }

    wizard.querySelector("[data-summary-period]").textContent =
      `${formatMonth(effectiveStart)} – ${formatMonth(effectiveEnd)}`;
    const automaticNoteRow = wizard.querySelector("[data-summary-automatic-note-row]");
    const automaticNoteText = wizard.querySelector("[data-summary-automatic-note]");
    automaticNoteText.textContent = automaticNote;
    automaticNoteRow.classList.toggle("hidden", !automaticNote);
    automaticNoteRow.classList.toggle("flex", Boolean(automaticNote));
    wizard.querySelector("[data-summary-duration]").textContent =
      `${state.includedMonthCount ?? countMonthsInclusive(effectiveStart, effectiveEnd)} ay`;
  };

  const updateFileSummaries = () => {
    wizard.querySelectorAll("[data-summary-asset]").forEach((element) => {
      element.textContent = state.assetFileName ?? "—";
    });

    wizard.querySelectorAll("[data-summary-index]").forEach((element) => {
      element.textContent = state.indexFileName ?? "—";
    });
  };

  fileInputs.forEach((input) => {
    input.addEventListener("change", async () => {
      if (state.fileUploadInProgress) {
        input.value = "";
        return;
      }

      setUploadControlsDisabled(true);

      try {
        await setFileState(input);
      } finally {
        setUploadControlsDisabled(false);
      }
    });
  });

  wizard.querySelectorAll("[data-file-clear]").forEach((button) => {
    button.addEventListener("click", async () => {
      if (state.reportLocked) {
        return;
      }

      const kind = button.dataset.fileKind;
      const row = wizard.querySelector(`[data-file-row="${kind}"]`);
      const input = row?.querySelector(`[data-file-input="${kind}"]`);
      const name = row?.querySelector("[data-file-name]");
      const status = row?.querySelector("[data-file-status]");
      const validation = row?.querySelector("[data-file-validation]");
      const body = new URLSearchParams();

      if (antiforgeryToken) {
        body.set("__RequestVerificationToken", antiforgeryToken);
      }

      button.disabled = true;

      try {
        const response = await fetch(button.dataset.fileClearUrl, {
          method: "POST",
          headers: { "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8" },
          body,
        });

        if (!response.ok) {
          throw new Error("Dosya kaldırılamadı.");
        }

        if (input) {
          input.value = "";
        }

        name.textContent = "Henüz dosya seçilmedi";
        name.classList.add("text-slate-400");
        name.classList.remove("text-ink-900");
        status.hidden = true;
        status.classList.add("hidden");
        setValidationMessage(validation, "");
        setClearButtonVisibility(row, false);

        if (kind === "assetFile") {
          state.assetFile = null;
          state.assetFileName = null;
          state.assetValidationRequestId++;
          state.assetValidationState = "idle";
          state.assetFirstMonth = null;
          state.assetLastMonth = null;
        } else {
          state.indexFile = null;
          state.indexFileName = null;
          state.indexValidationRequestId++;
          state.indexValidationState = "idle";
        }

        resetWizardRange();
        updateFileSummaries();
        updateContinueState();
      } catch {
        setValidationMessage(
          validation,
          "Dosya kaldırılamadı. Lütfen tekrar deneyin.",
          "negative",
        );
        button.disabled = false;
      }
    });
  });

  wizard.querySelectorAll("[data-go-step]").forEach((button) => {
    button.addEventListener("click", () => {
      const nextStep = Number(button.dataset.goStep);

      if (nextStep === 2 && !(
        state.assetValidationState === "valid" &&
        state.indexValidationState === "valid"
      )) {
        return;
      }

      if (nextStep === 3 && state.rangeValidationState !== "valid") {
        void validateWizardRange();
        return;
      }

      state.step = nextStep;
      writeWizardStorage(wizardStorageKeys.step, String(nextStep));
      updateFileSummaries();

      if (nextStep === 2) {
        void validateWizardRange();
      }

      if (nextStep === 3) {
        updateConfirmationSummary();
      }

      closeMonthPickers();
      renderStep();
      wizard.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  });

  updateFileSummaries();
  updateContinueState();
  updateRangeContinueState();
  renderStep();

  if (state.step === 2) {
    void validateWizardRange().then((isValid) => {
      if (isValid && requestedInitialStep === 3) {
        state.step = 3;
        updateConfirmationSummary();
        renderStep();
      }
    });
  }

  if (state.reportLocked) {
    wizard.querySelectorAll("button, input").forEach((control) => {
      control.disabled = true;
    });
  }
});
