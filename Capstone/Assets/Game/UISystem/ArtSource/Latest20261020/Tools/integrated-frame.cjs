// One continuous variable-inset molding. No raster corner fragments or repeated stamps.
const G=require('./geometry.cjs');const D=require('./edge-stripes.cjs');const assets=[];let serial=0;
const C={warm:'#D8C6A7',shadow:'#293750',deep:'#27324D'};
const add=(a,b)=>[a[0]+b[0],a[1]+b[1]],sub=(a,b)=>[a[0]-b[0],a[1]-b[1]],mul=(a,n)=>[a[0]*n,a[1]*n],cross=(a,b)=>a[0]*b[1]-a[1]*b[0],length=a=>Math.hypot(...a),unit=a=>mul(a,1/length(a)),normal=u=>[-u[1],u[0]],round=n=>Math.round(n*10000)/10000;
const pathOf=p=>'M'+p.map(q=>q.map(round).join(',')).join('L')+'Z';
function pts(type,w,h,p=3){return G.pts(type,w,h,p);}
function intersection(a,u,b,v){const d=cross(u,v);if(Math.abs(d)<1e-8)return mul(add(a,b),.5);return add(a,mul(u,cross(sub(b,a),v)/d));}
function unionPoly(i){return G.unionPoly(i);}
const unionPath=i=>pathOf(unionPoly(i));
function profile(poly,role,w,h){const base=role==='slot'?1.6:role==='button'?2.25:role==='light'?2.8:role==='union'?3.4:4.1,cap=role==='slot'?2.8:role==='button'?3.3:role==='light'?4.9:role==='union'?5.7:7.2,blend=role==='slot'?9:role==='button'?15:28;
 const distances=poly.map((v,i)=>{if(role!=='union')return cap;const prev=poly[(i+poly.length-1)%poly.length],next=poly[(i+1)%poly.length],turn=cross(sub(v,prev),sub(next,v));if(turn<0)return base;if(v[1]>=h-22)return cap;if(v[1]<=4)return 4.4;return base;});
 const stations=[];for(let i=0;i<poly.length;i++){const v=poly[i],next=poly[(i+1)%poly.length],u=unit(sub(next,v)),len=length(sub(next,v));stations.push({point:v,index:i,distance:distances[i],vertex:true});if(len>blend*3){const n=normal(u),l=Math.min(blend,len/3);stations.push({point:add(v,mul(u,l)),normal:n,distance:base,vertex:false});stations.push({point:add(next,mul(u,-l)),normal:n,distance:base,vertex:false});}}
 function inset(f){return stations.map(s=>{if(!s.vertex)return add(s.point,mul(s.normal,s.distance*f));const i=s.index,prev=poly[(i+poly.length-1)%poly.length],next=poly[(i+1)%poly.length],u=unit(sub(s.point,prev)),v=unit(sub(next,s.point)),d=s.distance*f;return intersection(add(s.point,mul(normal(u),d)),u,add(s.point,mul(normal(v),d)),v);});}
 return {base,cap,stations,inset};}
function band(a,b,fill,opacity=1){return `<path d="${pathOf(a)}${pathOf(b)}" fill="${fill}" fill-rule="evenodd" opacity="${opacity}"/>`;}
function mask(p,w,h,body){const id='trim'+(++serial);return `<defs><mask id="${id}" maskUnits="userSpaceOnUse" x="0" y="0" width="${w}" height="${h}"><path d="${p}" fill="white"/></mask></defs><g mask="url(#${id})">${body}</g>`;}
function engraving(poly,role,w,h){if(role==='button'||role==='union')return '';let s='';for(let i=0;i<poly.length;i++){const a=poly[i],b=poly[(i+1)%poly.length],u=unit(sub(b,a)),len=length(sub(b,a));if(len>30||len<14)continue;const mid=mul(add(a,b),.5);if(!((mid[0]<w*.25&&mid[1]<h*.25)||(mid[0]>w*.75&&mid[1]>h*.75)))continue;const n=normal(u),center=add(mid,mul(n,3.4)),p=[add(center,mul(u,-4)),add(center,mul(n,1.05)),add(center,mul(u,4)),add(center,mul(n,-1.05))];s+=`<path d="${pathOf(p)}" fill="${C.deep}" opacity=".74"/><path d="M${p[0].map(round).join(',')}L${p[1].map(round).join(',')}L${p[2].map(round).join(',')}" fill="none" stroke="${C.warm}" stroke-width=".48" opacity=".62"/>`;}
 return s;}
function trim(poly,w,h,role){const id='metal'+(++serial),q=profile(poly,role,w,h),a=q.inset(0),b=q.inset(.18),c=q.inset(.82),d=q.inset(1);let body=`<defs><linearGradient id="${id}edge" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#CFCCCF"/><stop offset=".42" stop-color="#ABADC0"/><stop offset="1" stop-color="#68728D"/></linearGradient><linearGradient id="${id}face" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#9DA8BC"/><stop offset=".25" stop-color="#71829E"/><stop offset=".72" stop-color="#4D5E83"/><stop offset="1" stop-color="#3A496E"/></linearGradient></defs>`;
 body+=band(a,b,`url(#${id}edge)`)+band(b,c,`url(#${id}face)`)+band(c,d,C.shadow,.86)+engraving(poly,role,w,h)+(role==='slot'?'':D.details(poly,w,h,role));
 return `<g data-frame-construction="shared-contour-variable-inset" data-role="${role}">${mask(pathOf(poly),w,h,body)}</g>`;}
function frame(x,y,w,h,type='panel',light=false){return `<g transform="translate(${x} ${y})">${trim(pts(type,w,h),w,h,light?'light':'panel')}</g>`;}
function button(x,y,w,h){return `<g transform="translate(${x} ${y})">${trim(pts('parallel',w,h),w,h,'button')}</g>`;}
function unionFrame(x,y,i){return `<g transform="translate(${x} ${y})">${trim(unionPoly(i),864,620,'union')}</g>`;}
function decorate(x,y,w,h,type,color){if(type==='panel'&&w>=650&&h>=400)return frame(x,y,w,h);if(type==='panel'&&w>=1300&&h>=130)return frame(x,y,w,h,'panel',true);if(type==='parallel'&&w>=150&&h>=44)return button(x,y,w,h);return '';}
module.exports={assets,frame,button,unionFrame,unionPath,unionPoly,pts,pathOf,decorate,profile,trim};
