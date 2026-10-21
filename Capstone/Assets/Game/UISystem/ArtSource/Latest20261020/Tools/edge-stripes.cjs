// Deliberate second rim on RIGHT + BOTTOM only, all inside the shared silhouette.
let seq=0;
const sub=(a,b)=>[a[0]-b[0],a[1]-b[1]],cross=(a,b)=>a[0]*b[1]-a[1]*b[0],unit=a=>{const n=Math.hypot(...a);return a.map(v=>v/n)},normal=a=>[-a[1],a[0]],add=(a,b)=>a.map((v,i)=>v+b[i]),mul=(a,s)=>a.map(v=>v*s),r=v=>Math.round(v*1000)/1000,pstr=p=>p.map(r).join(',');
function inset(p,d){return p.map((v,i)=>{const u=unit(sub(v,p[(i+p.length-1)%p.length])),w=unit(sub(p[(i+1)%p.length],v)),a=add(v,mul(normal(u),d)),b=add(v,mul(normal(w),d)),det=cross(u,w);return Math.abs(det)<1e-8?a:add(a,mul(u,cross(sub(b,a),w)/det));});}
function doubleEdge(poly,w,h,role){
 const distance=role==='button'?4:role==='union'?7:role==='light'?7.5:10,q=inset(poly,distance),run=[];
 for(let i=0;i<poly.length;i++){const a=poly[i],b=poly[(i+1)%poly.length],dx=b[0]-a[0],dy=b[1]-a[1];if((Math.abs(dx)<.01&&dy>0&&a[0]>w*.7)||(Math.abs(dy)<.01&&dx<0&&a[1]>h*.8)||(dx<0&&dy>0&&a[0]>w*.7&&b[1]>h*.7))run.push(i);}
 const first=run[0],last=(run.at(-1)+1)%poly.length,points=[poly[first],q[first],...run.map(i=>q[(i+1)%poly.length]),poly[last]];
 return {path:'M'+points.map(pstr).join('L'),inset:distance,points,edges:['right','bottom'],outerConnections:[poly[first],poly[last]]};
}
function ribbon(points,width){
 const side=sign=>points.map((v,i)=>{const d=i===0||i===points.length-1?0:width/2;if(!d)return v;const u=unit(sub(v,points[i-1])),z=unit(sub(points[i+1],v)),a=add(v,mul(normal(u),d*sign)),b=add(v,mul(normal(z),d*sign)),det=cross(u,z);if(Math.abs(det)<1e-8)return a;const q=add(a,mul(u,cross(sub(b,a),z)/det));return Math.hypot(...sub(q,v))>width*2?add(v,mul(unit(add(normal(u),normal(z))),d*sign)):q;});
 return 'M'+side(1).concat(side(-1).reverse()).map(pstr).join('L')+'Z';
}
function details(poly,w,h,role){
 const id='alloy'+(++seq),g=`url(#${id})`,edge=doubleEdge(poly,w,h,role),thin=role==='button';
 let s=`<defs><linearGradient id="${id}" x2="1" y2="1"><stop stop-color="#E0D2B5"/><stop offset=".5" stop-color="#BAA48A"/><stop offset="1" stop-color="#8C91A8"/></linearGradient></defs>`;
 // A dark spacer and a bevel glint make the second layer readable against the face.
 s+=`<g data-secondary-edge="${edge.path}"><path d="${ribbon(edge.points,thin?2.6:3.4)}" fill="#303B58" opacity=".72"/><path d="${ribbon(edge.points,thin?1:1.3)}" fill="${g}" opacity=".92"/></g>`;
 let x0=poly[0][0]+10,y0=3,count=thin?2:3;
 if(role==='union'){x0=Math.min(...poly.filter(p=>p[1]<=3.01).map(p=>p[0]))+17;y0=3;}
 // One cluster at the upper-left: short diagonal engraved stripes, no leaf stickers.
 for(let i=0;i<count;i++){const x=x0+i*(thin?7:9),dh=thin?5:9;s+=`<path d="M${r(x)},${y0}h4l-${dh},${dh}h-4Z" fill="${g}" opacity="${thin?.7:.8}"/>`;}
 // Three short ribs in the right-edge gutter, deliberately confined near the lower corner.
 if(!thin){for(let i=0;i<3;i++){const y=h-61+i*10;s+=`<path d="M${w-12},${y+4}L${w-4},${y-1}V${y+2}L${w-12},${y+7}Z" fill="${g}" opacity=".74"/>`;}}
 else {const x=w-poly[0][0]-16;s+=`<path d="M${r(x)},${h-3}h-5l6,-5h5Z" fill="${g}" opacity=".78"/>`;}
 return `<g data-gaming-detail="right-bottom-double-edge-and-stripes" pointer-events="none">${s}</g>`;
}
module.exports={details,doubleEdge,ribbon};
