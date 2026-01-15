const API_BASE = "http://127.0.0.1:8080";

function setQuery(sql) {
  document.getElementById("sql-input").value = sql;
}

async function fetchStatus() {
  try {
    const res = await fetch(`${API_BASE}/v1/status`);
    if (!res.ok) return;
    const data = await res.json();
    document.getElementById("stat-tx").innerText = data.active_transactions || 0;
    document.getElementById("stat-ts").innerText = data.current_timestamp || 1000;
    const memMb = ((data.memory_bytes || 0) / (1024 * 1024)).toFixed(1);
    document.getElementById("stat-mem").innerText = `${memMb} MB`;
    if (data.version) {
      document.getElementById("version-badge").innerText = `v${data.version}`;
    }
  } catch (err) {
    // Offline / detached mode
  }
}

async function fetchCatalog() {
  try {
    const res = await fetch(`${API_BASE}/v1/catalog`);
    if (!res.ok) return;
    const data = await res.json();
    const list = document.getElementById("table-list");
    list.innerHTML = "";

    if (!data.tables || data.tables.length === 0) {
      list.innerHTML = '<li class="table-item" style="color: var(--text-secondary);">No tables found</li>';
      return;
    }

    data.tables.forEach(tbl => {
      const li = document.createElement("li");
      li.className = "table-item";
      li.innerText = `${tbl.table_name} (${tbl.columns ? tbl.columns.length : 0} cols)`;
      li.onclick = () => setQuery(`SELECT * FROM ${tbl.table_name} LIMIT 10;`);
      list.appendChild(li);
    });
  } catch (err) {
    // Server not running
  }
}

async function runQuery() {
  const sql = document.getElementById("sql-input").value.trim();
  if (!sql) return;

  const btn = document.getElementById("btn-run");
  const statusLbl = document.getElementById("query-status");
  btn.disabled = true;
  statusLbl.innerText = "Executing...";

  try {
    const res = await fetch(`${API_BASE}/v1/query`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sql })
    });

    const data = await res.json();
    if (!res.ok) {
      statusLbl.innerText = "Error";
      alert(`Query execution failed: ${data.error || "Unknown error"}`);
      return;
    }

    statusLbl.innerText = "Completed";
    document.getElementById("stat-lat").innerText = `${data.execution_time_ms.toFixed(2)} ms`;
    document.getElementById("results-time").innerText = `${data.execution_time_ms.toFixed(2)} ms`;
    document.getElementById("results-count").innerText = `Results (${data.rows.length} rows)`;

    renderTable(data.columns, data.rows);
    renderPlanDag(data.explain || "LogicalScan -> LogicalProject");
    renderVectorScatter(data.columns, data.rows);
    fetchCatalog();
    fetchStatus();
  } catch (err) {
    statusLbl.innerText = "Connection error";
    // Render mock sample table for offline visualization
    renderSampleData();
  } finally {
    btn.disabled = false;
  }
}

function renderTable(columns, rows) {
  const table = document.getElementById("results-table");
  const thead = table.querySelector("thead");
  const tbody = table.querySelector("tbody");

  thead.innerHTML = "";
  tbody.innerHTML = "";

  if (!columns || columns.length === 0) return;

  const trHead = document.createElement("tr");
  columns.forEach(col => {
    const th = document.createElement("th");
    th.innerText = col;
    trHead.appendChild(th);
  });
  thead.appendChild(trHead);

  rows.forEach(r => {
    const tr = document.createElement("tr");
    r.forEach(cell => {
      const td = document.createElement("td");
      if (Array.isArray(cell)) {
        td.innerText = `[${cell.slice(0, 3).map(v => typeof v === "number" ? v.toFixed(2) : v).join(", ")}... (${cell.length}d)]`;
      } else if (cell === null || cell === undefined) {
        td.innerText = "NULL";
        td.style.color = "var(--text-secondary)";
      } else {
        td.innerText = cell;
      }
      tr.appendChild(td);
    });
    tbody.appendChild(tr);
  });
}

function renderPlanDag(explainText) {
  const svg = document.getElementById("plan-svg");
  svg.innerHTML = "";

  const nodes = [
    { title: "TableScan", x: 40, y: 50 },
    { title: "Filter", x: 140, y: 50 },
    { title: "VectorKnn", x: 240, y: 50 },
    { title: "Project", x: 340, y: 50 }
  ];

  for (let i = 0; i < nodes.length - 1; i++) {
    const line = document.createElementNS("http://www.w3.org/2000/svg", "line");
    line.setAttribute("x1", nodes[i].x + 40);
    line.setAttribute("y1", nodes[i].y + 15);
    line.setAttribute("x2", nodes[i + 1].x);
    line.setAttribute("y2", nodes[i + 1].y + 15);
    line.setAttribute("stroke", "#30363d");
    line.setAttribute("stroke-width", "2");
    svg.appendChild(line);
  }

  nodes.forEach(n => {
    const rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
    rect.setAttribute("x", n.x);
    rect.setAttribute("y", n.y);
    rect.setAttribute("width", 75);
    rect.setAttribute("height", 30);
    rect.setAttribute("rx", 5);
    rect.setAttribute("fill", "#21262d");
    rect.setAttribute("stroke", "#58a6ff");
    rect.setAttribute("stroke-width", "1");
    svg.appendChild(rect);

    const text = document.createElementNS("http://www.w3.org/2000/svg", "text");
    text.setAttribute("x", n.x + 37);
    text.setAttribute("y", n.y + 19);
    text.setAttribute("fill", "#c9d1d9");
    text.setAttribute("font-size", "11");
    text.setAttribute("font-family", "monospace");
    text.setAttribute("text-anchor", "middle");
    text.textContent = n.title;
    svg.appendChild(text);
  });
}

function renderVectorScatter(columns, rows) {
  const svg = document.getElementById("vector-svg");
  svg.innerHTML = "";

  const vecColIdx = columns.findIndex(c => c.toLowerCase().includes("emb") || c.toLowerCase().includes("vec"));
  const points = [];

  if (vecColIdx >= 0) {
    rows.forEach((r, idx) => {
      const v = r[vecColIdx];
      if (Array.isArray(v) && v.length >= 2) {
        points.push({ x: v[0], y: v[1], label: `Row ${idx + 1}` });
      }
    });
  }

  if (points.length === 0) {
    // Generate synthetic decorative clusters for empty results
    for (let i = 0; i < 20; i++) {
      points.push({
        x: Math.sin(i) * 0.4 + 0.5,
        y: Math.cos(i) * 0.4 + 0.5,
        label: `Vector ${i + 1}`
      });
    }
  }

  points.forEach(p => {
    const cx = 30 + p.x * 280;
    const cy = 20 + p.y * 130;

    const circle = document.createElementNS("http://www.w3.org/2000/svg", "circle");
    circle.setAttribute("cx", cx);
    circle.setAttribute("cy", cy);
    circle.setAttribute("r", 5);
    circle.setAttribute("fill", "#bc8cff");
    circle.setAttribute("opacity", "0.85");

    const title = document.createElementNS("http://www.w3.org/2000/svg", "title");
    title.textContent = p.label;
    circle.appendChild(title);

    svg.appendChild(circle);
  });
}

function renderSampleData() {
  const cols = ["id", "title", "domain", "views", "embedding"];
  const rows = [
    [1, "Relational Storage Systems", "Database", 1420, [0.82, 0.15, 0.05, 0.10]],
    [2, "Transformer Attention Graphs", "MachineLearning", 3590, [0.12, 0.91, 0.22, 0.05]],
    [3, "Zero Knowledge Arguments", "Cryptography", 2180, [0.05, 0.18, 0.88, 0.25]]
  ];
  renderTable(cols, rows);
  renderPlanDag();
  renderVectorScatter(cols, rows);
}

// Auto-poll telemetry every 3 seconds
setInterval(fetchStatus, 3000);
document.addEventListener("DOMContentLoaded", () => {
  fetchStatus();
  fetchCatalog();
  renderPlanDag();
  renderVectorScatter([], []);
});
