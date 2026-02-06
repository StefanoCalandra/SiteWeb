class KpiCard extends HTMLElement {
  static get observedAttributes() {
    return ["title", "value"];
  }

  constructor() {
    super();
    this.attachShadow({ mode: "open" });
  }

  connectedCallback() {
    this.render();
  }

  attributeChangedCallback() {
    this.render();
  }

  render() {
    // Web Component con Shadow DOM e stile isolato.
    const title = this.getAttribute("title") ?? "";
    const value = this.getAttribute("value") ?? "0";

    this.shadowRoot.innerHTML = `
      <style>
        .card {
          background: var(--kpi-bg);
          color: var(--kpi-fg);
          padding: 1.25rem;
          border-radius: 12px;
          box-shadow: 0 10px 25px rgba(15, 23, 42, 0.2);
        }
        .value {
          font-size: 2rem;
          color: var(--kpi-accent);
        }
      </style>
      <div class="card">
        <div>${title}</div>
        <div class="value">${value}</div>
      </div>
    `;
  }
}

customElements.define("kpi-card", KpiCard);
