function createPhotoDropzone(box, config) {
 const file=document.createElement('input');
 file.type='file';file.accept=config.accept;file.id='media-file';file.hidden=true;
 file.onchange=()=>{acceptPhoto(file.files?.[0]);file.value=''};
 const zone=document.createElement('button');
 zone.type='button';zone.id='photo-dropzone';zone.className='photo-dropzone';
 zone.onclick=()=>file.click();
 let dragDepth=0;
 zone.addEventListener('dragenter',event=>{event.preventDefault();dragDepth++;zone.classList.add('dragging')});
 zone.addEventListener('dragover',event=>{event.preventDefault();event.dataTransfer.dropEffect='copy'});
 zone.addEventListener('dragleave',()=>{if(--dragDepth<=0)zone.classList.remove('dragging')});
 zone.addEventListener('drop',event=>{event.preventDefault();dragDepth=0;zone.classList.remove('dragging');if(event.dataTransfer.files.length!==1){photoMessage('Choose one photo at a time.',true);return}acceptPhoto(event.dataTransfer.files[0])});
 const status=document.createElement('p');status.id='photo-status';status.className='photo-status';status.setAttribute('role','status');status.textContent='JPG, PNG, GIF or WebP · Up to 20 MB · Local preview';
 box.append(file,zone,status);refreshPhotoDropzone();
}
function photoMessage(text,error=false){const status=$('photo-status');if(status){status.textContent=text;status.classList.toggle('is-error',error)}}
function refreshPhotoDropzone(){
 const zone=$('photo-dropzone');if(!zone)return;
 const url=localMedia.photo||properties().photo?.[0];
 zone.replaceChildren();zone.classList.remove('has-photo');
 zone.setAttribute('aria-label','Upload a photo. Choose a file or drop it here.');
 const empty=document.createElement('span');empty.className='dropzone-empty';
 empty.innerHTML='<svg viewBox="0 0 32 32" aria-hidden="true"><rect x="4" y="5" width="24" height="22" rx="3"/><circle cx="11" cy="12" r="2"/><path d="m5 24 8-8 5 5 4-5 6 7"/></svg><strong>Drop a photo here</strong><span>or click to choose a file</span>';
 zone.append(empty);
 if(!url||(!localMedia.photo&&!safeMedia(url)))return;
 const image=document.createElement('img');image.alt=properties().alt?.[0]||'Selected photo';image.draggable=false;
 const overlay=document.createElement('span');overlay.className='photo-change';overlay.textContent='Change photo';
 image.onload=()=>{if(!image.isConnected)return;empty.hidden=true;zone.classList.add('has-photo');zone.setAttribute('aria-label','Change photo. Choose a replacement or drop it here.')};
 image.onerror=()=>{if(!image.isConnected)return;image.remove();overlay.remove();photoMessage('Couldn’t load this photo. Drop a replacement here or check its URL.',true)};
 zone.append(image,overlay);image.src=url;
}
let photoSelection=0;
async function acceptPhoto(file){
 if(!file)return;
 if(!contentTypes.photo.accept.split(',').includes(file.type)||file.size===0||file.size>20*1024*1024){photoMessage('Choose a JPG, PNG, GIF or WebP file up to 20 MB. Your current photo is unchanged.',true);return}
 const selection=++photoSelection,objectUrl=URL.createObjectURL(file);
 photoMessage('Preparing photo…');
 const probe=new Image();probe.src=objectUrl;
 try{await probe.decode()}catch{URL.revokeObjectURL(objectUrl);if(selection===photoSelection)photoMessage('This file could not be opened as a photo. Try another image.',true);return}
 if(selection!==photoSelection||type!=='photo'){URL.revokeObjectURL(objectUrl);return}
 if(localMedia.photo)URL.revokeObjectURL(localMedia.photo);
 localMedia.photo=objectUrl;mediaObjectUrls.push(objectUrl);
 properties().photo=['/media/concept/'+encodeURIComponent(file.name)];
 $('property-photo').value=properties().photo[0];
 refreshPhotoDropzone();changed();
 photoMessage(file.name+' · '+probe.naturalWidth+' × '+probe.naturalHeight+' · Ready in this preview');
 $('photo-dropzone').focus();
}
