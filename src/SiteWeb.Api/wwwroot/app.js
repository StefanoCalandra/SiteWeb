import "./components.js";

const status = document.getElementById("status");
const connectButton = document.getElementById("connect");
const disconnectButton = document.getElementById("disconnect");

let connection = null;
let queue = [];
let reconnectDelay = 1000;

function updateStatus(text) {
  status.textContent = `Stato: ${text}`;
}

async function connect() {
  // Inizializza connessione SignalR con riconnessione automatica.
  updateStatus("connecting...");
  connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/dashboard")
    .withAutomaticReconnect([0, 1000, 2000, 5000])
    .build();

  connection.on("kpi", (payload) => {
    // Aggiorna le KPI lato UI in tempo reale.
    const cards = document.querySelectorAll("kpi-card");
    cards.forEach((card) => {
      if (card.getAttribute("title") === payload.name) {
        card.setAttribute("value", payload.value);
      }
    });
  });

  connection.onreconnecting(() => {
    updateStatus("reconnecting...");
  });

  connection.onreconnected(() => {
    updateStatus("online");
    flushQueue();
  });

  connection.onclose(() => {
    updateStatus("offline");
    scheduleReconnect();
  });

  await connection.start();
  updateStatus("online");
}

function scheduleReconnect() {
  if (!connection) return;
  // Backoff semplice per tentare nuove connessioni.
  setTimeout(async () => {
    try {
      await connection.start();
      updateStatus("online");
      reconnectDelay = 1000;
    } catch {
      reconnectDelay = Math.min(reconnectDelay * 2, 10000);
      scheduleReconnect();
    }
  }, reconnectDelay);
}

function flushQueue() {
  // Placeholder per gestire eventuali eventi locali in coda.
  queue = [];
}

connectButton.addEventListener("click", () => {
  connect();
});

disconnectButton.addEventListener("click", async () => {
  if (connection) {
    await connection.stop();
  }
});

if ("serviceWorker" in navigator) {
  // Registra il service worker per caching offline.
  navigator.serviceWorker.register("/service-worker.js");
}
