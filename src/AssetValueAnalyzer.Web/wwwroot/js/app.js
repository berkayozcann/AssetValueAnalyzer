document.addEventListener("DOMContentLoaded", () => {
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
      status.textContent = "Seçildi";
      status.classList.remove("text-slate-400", "border-slate-600");
      status.classList.add("text-positive-400", "border-positive-400/45");
    } else {
      name.textContent = "Dosya bekleniyor";
      name.classList.add("text-slate-400");
      name.classList.remove("text-white");
      status.textContent = "Bekliyor";
      status.classList.add("text-slate-400", "border-slate-600");
      status.classList.remove("text-positive-400", "border-positive-400/45");
    }

    continueButton.disabled = !(state.assetFile && state.indexFile);
  };

  const renderStep = () => {
    panels.forEach((panel) => {
      const panelStep = Number(panel.dataset.stepPanel);
      panel.hidden = panelStep !== state.step;
    });

    markers.forEach((marker) => {
      const markerStep = Number(marker.dataset.stepMarker);
      const circle = marker.querySelector("[data-step-circle]");
      const check = marker.querySelector("[data-step-check]");
      const number = marker.querySelector("[data-step-number]");
      const label = marker.querySelector("[data-step-label]");

      circle.classList.toggle("border-accent-300", markerStep <= state.step);
      circle.classList.toggle("bg-accent-300", markerStep === state.step);
      circle.classList.toggle("text-canvas-950", markerStep === state.step);
      circle.classList.toggle("bg-positive-400/10", markerStep < state.step);
      label.classList.toggle("text-white", markerStep <= state.step);
      label.classList.toggle("text-slate-400", markerStep > state.step);
      check.hidden = markerStep >= state.step;
      number.hidden = markerStep < state.step;
    });

    connectors.forEach((connector) => {
      const connectorStep = Number(connector.dataset.stepConnector);
      connector.classList.toggle("bg-accent-300", connectorStep < state.step);
      connector.classList.toggle("bg-slate-600/70", connectorStep >= state.step);
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

      state.step = nextStep;
      updateFileSummaries();

      if (nextStep === 3) {
        const startMonth = wizard.querySelector("#startMonth").value;
        const endMonth = wizard.querySelector("#endMonth").value;
        wizard.querySelector("[data-summary-period]").textContent =
          startMonth && endMonth ? `${startMonth} – ${endMonth}` : "Dosyadaki ilk ve son ay";
      }

      renderStep();
      wizard.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  });

  renderStep();
});
