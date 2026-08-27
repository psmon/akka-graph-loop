namespace AkkaGraphLoop.Core.Pdsa;

/// <summary>
/// PDSA 그래프 뷰어 페이지(자체 포함: 외부 CDN 없이 vanilla JS + SVG 포스 레이아웃).
/// 독립 실행 <c>AkkaGraphLoop.Viewer</c> 와 <c>pdsa view</c> 인프로세스 서버가 공유한다.
/// </summary>
public static class PdsaViewerHtml
{
    public const string Page = """
<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>PDSA 그래프 뷰어 · Kùzu</title>
<style>
  :root { color-scheme: dark; }
  * { box-sizing: border-box; }
  body { margin:0; font-family: system-ui, "Segoe UI", sans-serif; background:#0f1420; color:#e6e9f0; height:100vh; display:flex; flex-direction:column; }
  header { padding:10px 16px; border-bottom:1px solid #232a3b; display:flex; align-items:center; gap:14px; flex-wrap:wrap; }
  header h1 { font-size:15px; margin:0; font-weight:700; color:#8fd0ff; }
  header .sub { font-size:12px; color:#8892a6; }
  header .spacer { flex:1; }
  button { background:#1c2438; color:#e6e9f0; border:1px solid #2c374f; border-radius:6px; padding:6px 12px; cursor:pointer; font-size:13px; }
  button:hover { background:#26314c; }
  select { background:#1c2438; color:#e6e9f0; border:1px solid #2c374f; border-radius:6px; padding:5px 8px; font-size:13px; }
  header .proj { font-size:13px; color:#8fd0ff; font-weight:600; }
  header .db { font-size:11px; color:#66708a; max-width:340px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
  header .hit { font-size:12px; color:#c7d0e0; background:#1c2438; border:1px solid #2c374f; border-radius:6px; padding:4px 8px; }
  .legend { display:flex; gap:12px; font-size:12px; color:#aab3c6; align-items:center; }
  .legend .dot { display:inline-block; width:11px; height:11px; border-radius:50%; margin-right:5px; vertical-align:-1px; }
  main { flex:1; display:flex; min-height:0; }
  #stage { flex:1; position:relative; }
  svg { width:100%; height:100%; display:block; }
  #panel { width:260px; border-left:1px solid #232a3b; padding:14px; font-size:13px; overflow:auto; }
  #panel h2 { font-size:13px; margin:0 0 8px; color:#8fd0ff; }
  #panel .row { display:flex; justify-content:space-between; padding:4px 0; border-bottom:1px dashed #232a3b; }
  #panel .row b { color:#aab3c6; font-weight:500; }
  #msg { position:absolute; inset:0; display:flex; align-items:center; justify-content:center; text-align:center; padding:20px; color:#9aa4ba; font-size:14px; }
  .node-label { font-size:12px; fill:#e6e9f0; pointer-events:none; }
  .edge-label { font-size:10px; fill:#7f8aa3; pointer-events:none; }
</style>
</head>
<body>
<header>
  <h1>PDSA 그래프 뷰어</h1>
  <label class="sub">프로젝트 <select id="projectSel"></select></label>
  <span class="proj" id="projName"></span>
  <div class="legend">
    <span><span class="dot" style="background:#5b8cff"></span>Project/Plan</span>
    <span><span class="dot" style="background:#37c8c8"></span>Cycle</span>
    <span><span class="dot" style="background:#a06cff"></span>Act</span>
    <span>Study 판정:</span>
    <span><span class="dot" style="background:#37c871"></span>met</span>
    <span><span class="dot" style="background:#e6a53a"></span>partial</span>
    <span><span class="dot" style="background:#e05a5a"></span>unmet</span>
    <span><span class="dot" style="background:#e05a5a;border-radius:0"></span>보강(REINFORCES)</span>
  </div>
  <div class="spacer"></div>
  <span class="hit" id="hitRate"></span>
  <span class="db" id="dbPath" title=""></span>
  <button id="reload">↻ 새로고침</button>
  <button id="relayout">⟳ 레이아웃 재배치</button>
</header>
<main>
  <div id="stage">
    <svg id="svg">
      <defs>
        <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" fill="#5a6683"/>
        </marker>
      </defs>
      <g id="edges"></g>
      <g id="edgeLabels"></g>
      <g id="nodes"></g>
      <g id="labels"></g>
    </svg>
    <div id="msg" style="display:none"></div>
  </div>
  <div id="panel">
    <h2>노드 정보</h2>
    <div id="panelBody" style="color:#8892a6">노드를 클릭하세요.</div>
  </div>
</main>
<script>
const SVG="http://www.w3.org/2000/svg";
const svg=document.getElementById("svg");
const gEdges=document.getElementById("edges"), gEdgeLabels=document.getElementById("edgeLabels");
const gNodes=document.getElementById("nodes"), gLabels=document.getElementById("labels");
const msg=document.getElementById("msg");
const projectSel=document.getElementById("projectSel");
const projName=document.getElementById("projName");
const dbPath=document.getElementById("dbPath");
const hitRate=document.getElementById("hitRate");
let nodes=[], edges=[], W=800, H=600, dragging=null, raf=null, alpha=0;

// 프로젝트 목록을 채우고 현재 프로젝트를 선택한다.
async function loadProjects(){
  try{
    const data = await (await fetch("/api/projects")).json();
    const list = data.projects || [];
    projectSel.innerHTML="";
    if(list.length===0 && data.current){ list.push(data.current); }
    for(const name of list){
      const opt=document.createElement("option");
      opt.value=name; opt.textContent=name;
      if(name===data.current) opt.selected=true;
      projectSel.appendChild(opt);
    }
    if(list.length<=1) projectSel.disabled=true;
  }catch(e){ /* 목록 실패해도 그래프는 시도 */ }
}

function size(){ const r=svg.getBoundingClientRect(); W=r.width; H=r.height; svg.setAttribute("viewBox",`0 0 ${W} ${H}`); }
window.addEventListener("resize", size);

async function load(){
  size();
  const sel = projectSel.value ? "?project="+encodeURIComponent(projectSel.value) : "";
  let data;
  try { data = await (await fetch("/api/graph"+sel)).json(); }
  catch(e){ return showMsg("서버 응답을 읽지 못했습니다."); }
  // 헤더에 현재 프로젝트/DB 를 항상 표시(에러여도 어느 프로젝트인지 알 수 있게).
  if(data.project){ projName.textContent="▶ "+data.project; }
  if(data.db){ dbPath.textContent=data.db; dbPath.title=data.db; }
  if(data.hitRate && data.hitRate.total>0){
    const h=data.hitRate, pct=Math.round(100*h.met/h.total);
    hitRate.textContent=`기대충족률 ${h.met}/${h.total} (${pct}%)`;
  } else { hitRate.textContent="기대충족률 —"; }
  if(data.error){ return showMsg(data.error); }
  if(!data.nodes || data.nodes.length===0){ return showMsg("그래프가 비어 있습니다. 먼저 `-- pdsa` 로 데이터를 생성하세요."); }
  msg.style.display="none";
  initGraph(data);
}

function showMsg(t){ nodes=[]; edges=[]; render(); msg.textContent=t; msg.style.display="flex"; }

function initGraph(data){
  const byId={};
  nodes=data.nodes.map((n,i)=>{
    const a=2*Math.PI*i/data.nodes.length;
    const o={...n, x:W/2+Math.cos(a)*160, y:H/2+Math.sin(a)*140, vx:0, vy:0,
             converged:(n.props&&n.props.converged||"").toLowerCase()==="true"};
    byId[n.id]=o; return o;
  });
  edges=data.edges.map(e=>({...e, s:byId[e.from], t:byId[e.to]})).filter(e=>e.s&&e.t);
  // 루트 노드(Project/Run)는 위쪽 중앙에 고정 배치
  const root=nodes.find(n=>n.kind==="Project"||n.kind==="Run"); if(root){ root.x=W/2; root.y=90; }
  startSim();
}

// 시뮬레이티드 어닐링(d3-force 방식): alpha 를 1→0 으로 냉각하며 힘을 점점 약하게 준다.
// 냉각이 끝나면(또는 움직임이 미미하면) 애니메이션을 '정지'해 안정된 배치를 보여준다.
// 자석(척력)·용수철(NEXT/HAS_PHASE 연결)·중심 인력은 유지되며, 다만 무한히 흔들리지 않는다.
const ALPHA_MIN=0.02, ALPHA_DECAY=0.975, V_DECAY=0.82, V_MAX=12;
function startSim(a=1){ alpha=a; cancelAnimationFrame(raf);
  const step=()=>{
    physics();
    render();
    alpha*=ALPHA_DECAY;                       // 냉각
    if(alpha>ALPHA_MIN && !dragging){ raf=requestAnimationFrame(step); }
    else { alpha=0; for(const n of nodes){ n.vx=n.vy=0; } render(); }  // 수렴 → 정지
  };
  raf=requestAnimationFrame(step);
}

function physics(){
  const k=0.02, rep=9000, spring=120;
  for(const n of nodes){ n.fx=(W/2-n.x)*k*0.15; n.fy=(H/2-n.y)*k*0.15; }
  for(let i=0;i<nodes.length;i++)for(let j=i+1;j<nodes.length;j++){
    const a=nodes[i], b=nodes[j]; let dx=a.x-b.x, dy=a.y-b.y; let d2=dx*dx+dy*dy||0.01; let d=Math.sqrt(d2);
    const f=rep/d2; const ux=dx/d, uy=dy/d; a.fx+=ux*f; a.fy+=uy*f; b.fx-=ux*f; b.fy-=uy*f;
  }
  for(const e of edges){ let dx=e.t.x-e.s.x, dy=e.t.y-e.s.y; let d=Math.sqrt(dx*dx+dy*dy)||0.01;
    const f=(d-spring)*0.02; const ux=dx/d, uy=dy/d; e.s.fx+=ux*f; e.s.fy+=uy*f; e.t.fx-=ux*f; e.t.fy-=uy*f; }
  for(const n of nodes){ if(n===dragging) continue; if(n.kind==="Run"||n.kind==="Project"){ continue; }
    // 힘에 alpha 를 곱해 점점 약하게 → 냉각과 함께 정지. 속도 상한으로 폭주(진동) 방지.
    n.vx=(n.vx+n.fx*alpha)*V_DECAY; n.vy=(n.vy+n.fy*alpha)*V_DECAY;
    const sp=Math.hypot(n.vx,n.vy); if(sp>V_MAX){ n.vx=n.vx/sp*V_MAX; n.vy=n.vy/sp*V_MAX; }
    n.x+=n.vx; n.y+=n.vy;
    n.x=Math.max(40,Math.min(W-40,n.x)); n.y=Math.max(40,Math.min(H-40,n.y)); }
}

function clear(g){ while(g.firstChild) g.removeChild(g.firstChild); }
function el(name,attrs){ const e=document.createElementNS(SVG,name); for(const k in attrs) e.setAttribute(k,attrs[k]); return e; }
function nodeColor(n){
  if(n.kind==="Project"||n.kind==="Run") return "#5b8cff";
  if(n.kind==="Cycle") return "#37c8c8";
  if(n.kind==="Phase"){ const k=((n.props&&n.props.kind)||"").toLowerCase();
    // Study 노드는 판정(verdict)에 따라 색을 바꾼다: met=초록 partial=주황 unmet=빨강, 미판정=회색.
    if(k==="study"){ const v=((n.props&&n.props.verdict)||"").toLowerCase();
      return {met:"#37c871",partial:"#e6a53a",unmet:"#e05a5a"}[v]||"#8a94a8"; }
    return {plan:"#5b8cff",do:"#37c871",act:"#a06cff"}[k]||"#8a94a8"; }
  return "#8a94a8";
}

function render(){
  clear(gEdges); clear(gEdgeLabels); clear(gNodes); clear(gLabels);
  for(const e of edges){
    const stroke = e.type==="NEXT" ? "#6b78a0" : e.type==="REINFORCES" ? "#e05a5a" : "#39415c";
    const width  = (e.type==="NEXT"||e.type==="REINFORCES") ? 2.2 : 1.4;
    const line=el("line",{x1:e.s.x,y1:e.s.y,x2:e.t.x,y2:e.t.y,stroke:stroke,"stroke-width":width,"marker-end":"url(#arrow)"});
    if(e.type==="REINFORCES") line.setAttribute("stroke-dasharray","2 3");        // 보강: 빨강 촘촘한 점선
    else if(e.type!=="NEXT") line.setAttribute("stroke-dasharray","4 4");
    gEdges.appendChild(line);
    const mx=(e.s.x+e.t.x)/2, my=(e.s.y+e.t.y)/2;
    const t=el("text",{x:mx,y:my-4,"text-anchor":"middle",class:"edge-label"}); t.textContent=e.type; gEdgeLabels.appendChild(t);
  }
  for(const n of nodes){
    const color=nodeColor(n);
    let shape, labelDy;
    if(n.kind==="Run"||n.kind==="Project"){
      shape=el("rect",{x:n.x-40,y:n.y-18,width:80,height:36,rx:8,fill:color,stroke:"#a9c6ff","stroke-width":1.5}); labelDy=4;
    } else if(n.kind==="Cycle"){
      const r=n.converged?24:21;
      shape=el("circle",{cx:n.x,cy:n.y,r:r,fill:n.converged?"#37c871":color,stroke:"#c3cad8","stroke-width":1.5}); labelDy=r+16;
    } else { // Phase (또는 기타): 작은 원
      shape=el("circle",{cx:n.x,cy:n.y,r:14,fill:color,stroke:"#c3cad8","stroke-width":1.2}); labelDy=28;
    }
    shape.style.cursor="grab"; shape.addEventListener("mousedown",ev=>startDrag(ev,n)); shape.addEventListener("click",()=>showPanel(n));
    gNodes.appendChild(shape);
    const label=el("text",{x:n.x,y:n.y+labelDy,"text-anchor":"middle",class:"node-label"});
    label.textContent=n.label; gLabels.appendChild(label);
  }
}

function showPanel(n){
  const b=document.getElementById("panelBody");
  let html=`<div class="row"><b>id</b><span>${n.id}</span></div><div class="row"><b>kind</b><span>${n.kind}</span></div>`;
  for(const k in (n.props||{})) html+=`<div class="row"><b>${k}</b><span>${n.props[k]}</span></div>`;
  b.innerHTML=html;
}

function startDrag(ev,n){ dragging=n; cancelAnimationFrame(raf);
  const move=e=>{ const r=svg.getBoundingClientRect(); n.x=e.clientX-r.left; n.y=e.clientY-r.top; render(); };
  const up=()=>{ dragging=null; document.removeEventListener("mousemove",move); document.removeEventListener("mouseup",up); startSim(0.3); };  // 드래그 후엔 살짝만 재정렬
  document.addEventListener("mousemove",move); document.addEventListener("mouseup",up); ev.preventDefault();
}

document.getElementById("reload").addEventListener("click",load);
projectSel.addEventListener("change",load);   // 프로젝트 전환 → 재시작 없이 재조회
document.getElementById("relayout").addEventListener("click",()=>{ if(nodes.length){ nodes.forEach((n,i)=>{const a=2*Math.PI*i/nodes.length; n.x=W/2+Math.cos(a)*160; n.y=H/2+Math.sin(a)*140; n.vx=n.vy=0;}); const root=nodes.find(n=>n.kind==="Project"||n.kind==="Run"); if(root){root.x=W/2;root.y=90;} startSim(); }});
loadProjects().then(load);
</script>
</body>
</html>
""";
}
