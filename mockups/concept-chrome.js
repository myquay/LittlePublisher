const dropdowns=[...document.querySelectorAll('.dropdown')];
dropdowns.forEach(d=>d.querySelector('summary').addEventListener('click',()=>{if(!d.open)dropdowns.forEach(other=>{if(other!==d)other.open=false})}));
document.addEventListener('click',e=>{dropdowns.forEach(d=>{if(!d.contains(e.target))d.open=false})});
document.addEventListener('keydown',e=>{if(e.key==='Escape'){const open=dropdowns.find(d=>d.open);if(open){open.open=false;open.querySelector('summary').focus()}}});
function explain(title,body){document.querySelector('#dialog-title').textContent=title;document.querySelector('#dialog-body').textContent=body;document.querySelector('#demo-dialog').showModal()}
document.querySelectorAll('[data-demo]').forEach(b=>b.onclick=()=>explain(b.dataset.demo,'This is the proposed home for this action. This HTML concept is not connected to your account or publishing services.'));
document.querySelectorAll('[data-check]').forEach(b=>b.onclick=()=>explain(b.dataset.check+' connection','In the live app, the connection check and its result would appear here. No connection check is performed in this mockup.'));
document.querySelector('#refresh').onclick=()=>{document.querySelector('#activity-status').textContent='Sample activity refreshed · No live services connected'};
