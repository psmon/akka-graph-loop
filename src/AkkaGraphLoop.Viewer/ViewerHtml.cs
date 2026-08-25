namespace AkkaGraphLoop.Viewer;

/// <summary>뷰어 페이지(자체 포함: 외부 CDN 없이 vanilla JS + SVG 포스 레이아웃).</summary>
internal static class ViewerHtml
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
  <span class="sub">Kùzu 임베디드 그래프 DB · Deming PDSA</span>
  <div class="legend">
    <span><span class="dot" style="background:#5b8cff"></span>Run</span>
    <span><span class="dot" style="background:#8a94a8"></span>Cycle</span>
    <span><span class="dot" style="background:#37c871"></span>수렴(Converged)</span>
  </div>
  <div class="spacer"></div>
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
let nodes=[], edges=[], W=800, H=600, dragging=null, raf=null;

function size(){ const r=svg.getBoundingClientRect(); W=r.width; H=r.height; svg.setAttribute("viewBox",`0 0 ${W} ${H}`); }
window.addEventListener("resize", size);

async function load(){
  size();
  let data;
  try { data = await (await fetch("/api/graph")).json(); }
  catch(e){ return showMsg("서버 응답을 읽지 못했습니다."); }
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
  // Run 노드는 위쪽 중앙에 고정 배치
  const run=nodes.find(n=>n.kind==="Run"); if(run){ run.x=W/2; run.y=90; }
  startSim();
}

function startSim(){ let ticks=0; cancelAnimationFrame(raf);
  const step=()=>{ physics(); render(); if(++ticks<600 && !dragging) raf=requestAnimationFrame(step); else render(); };
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
  for(const n of nodes){ if(n===dragging) continue; if(n.kind==="Run"){ continue; }
    n.vx=(n.vx+n.fx)*0.85; n.vy=(n.vy+n.fy)*0.85; n.x+=n.vx; n.y+=n.vy;
    n.x=Math.max(40,Math.min(W-40,n.x)); n.y=Math.max(40,Math.min(H-40,n.y)); }
}

function clear(g){ while(g.firstChild) g.removeChild(g.firstChild); }
function el(name,attrs){ const e=document.createElementNS(SVG,name); for(const k in attrs) e.setAttribute(k,attrs[k]); return e; }

function render(){
  clear(gEdges); clear(gEdgeLabels); clear(gNodes); clear(gLabels);
  for(const e of edges){
    const line=el("line",{x1:e.s.x,y1:e.s.y,x2:e.t.x,y2:e.t.y,stroke:e.type==="NEXT"?"#6b78a0":"#39415c","stroke-width":e.type==="NEXT"?2.2:1.4,"marker-end":"url(#arrow)"});
    if(e.type!=="NEXT") line.setAttribute("stroke-dasharray","4 4");
    gEdges.appendChild(line);
    const mx=(e.s.x+e.t.x)/2, my=(e.s.y+e.t.y)/2;
    const t=el("text",{x:mx,y:my-4,"text-anchor":"middle",class:"edge-label"}); t.textContent=e.type; gEdgeLabels.appendChild(t);
  }
  for(const n of nodes){
    let shape;
    if(n.kind==="Run"){
      shape=el("rect",{x:n.x-38,y:n.y-18,width:76,height:36,rx:8,fill:"#5b8cff",stroke:"#a9c6ff","stroke-width":1.5});
    } else {
      const r=n.converged?24:20;
      shape=el("circle",{cx:n.x,cy:n.y,r:r,fill:n.converged?"#37c871":"#8a94a8",stroke:n.converged?"#b9f7d2":"#c3cad8","stroke-width":1.5});
    }
    shape.style.cursor="grab"; shape.addEventListener("mousedown",ev=>startDrag(ev,n)); shape.addEventListener("click",()=>showPanel(n));
    gNodes.appendChild(shape);
    const label=el("text",{x:n.x,y:n.kind==="Run"?n.y+4:n.y+ (n.converged?40:36),"text-anchor":"middle",class:"node-label"});
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
  const up=()=>{ dragging=null; document.removeEventListener("mousemove",move); document.removeEventListener("mouseup",up); startSim(); };
  document.addEventListener("mousemove",move); document.addEventListener("mouseup",up); ev.preventDefault();
}

document.getElementById("reload").addEventListener("click",load);
document.getElementById("relayout").addEventListener("click",()=>{ if(nodes.length){ nodes.forEach((n,i)=>{const a=2*Math.PI*i/nodes.length; n.x=W/2+Math.cos(a)*160; n.y=H/2+Math.sin(a)*140; n.vx=n.vy=0;}); const run=nodes.find(n=>n.kind==="Run"); if(run){run.x=W/2;run.y=90;} startSim(); }});
load();
</script>
</body>
</html>
""";
}
