const round=n=>Math.round(n*10000)/10000,skew=(h,p=3)=>Math.tan(Math.PI/15)*(h-2*p);
function pts(type,w,h,p=3){
 if(type==='square')return [[p,p],[w-p,p],[w-p,h-p],[p,h-p]];
 if(type==='diamond')return [[w/2,p],[w-p,h/2],[w/2,h-p],[p,h/2]];
 const cut=Math.min(type==='parallel'||type==='tab'?skew(h,p):type==='skill-oct'?Math.min(10,Math.max(5,Math.min(w,h)*.13)):type==='avatar'?10:w>=650&&h>=400?18:h<=64?8:12,(w-2*p)/4,(h-2*p)/3);
 const points=type==='parallel'?[[p+cut,p],[w-p,p],[w-p-cut,h-p],[p,h-p]]:type==='tab'?[[p+cut,p],[w-p-cut,p],[w-p,h-p],[p,h-p]]:[[p+cut,p],[w-p-cut,p],[w-p,p+cut],[w-p,h-p-cut],[w-p-cut,h-p],[p+cut,h-p],[p,h-p-cut],[p,p+cut]];
 return points.map(v=>v.map(round));
}
const cross=(a,b)=>a[0]*b[1]-a[1]*b[0],sub=(a,b)=>a.map((v,i)=>v-b[i]);
function intersection(a,u,b,v){const t=cross(sub(b,a),v)/cross(u,v);return a.map((n,i)=>round(n+t*u[i]));}
function tabCapPoly(j,active,root=68){
 const x=16+j*280,t=52,top=j===active?3:11,k=(t-top)*Math.tan(Math.PI/15),e=(root-t)*Math.tan(Math.PI/15);
 return [[x+k,top],[x+272-k,top],[x+272+e,root],[x-e,root]].map(v=>v.map(round));
}
function unionPoly(active){
 const w=864,h=620,p=3,c=18,t=52,lb=[p,t+c],lt=[p+c,t],rt=[w-p-c,t],rb=[w-p,t+c];
 const out=[lb];
 for(let j=0;j<3;j++){
  const x=16+j*280,top=j===active?3:11,k=(t-top)*Math.tan(Math.PI/15),bl=[x,t],tl=[round(x+k),top],tr=[round(x+272-k),top],br=[x+272,t];
  if(j===0)out.push(intersection(lb,sub(lt,lb),bl,sub(tl,bl)));
  else out.push(bl);
  out.push(tl,tr);
  if(j===2)out.push(intersection(rt,sub(rb,rt),tr,sub(br,tr)));
  else out.push(br);
 }
 out.push(rb,[w-p,h-p-c],[w-p-c,h-p],[p+c,h-p],[p,h-p-c]);
 return out;
}
const pathOf=p=>'M'+p.map(v=>v.map(round).join(',')).join('L')+'Z';
module.exports={pts,unionPoly,tabCapPoly,pathOf,skew,angle:12,tabDeck:{activeTop:3,inactiveTop:11,bodyTop:52,root:68,width:272,step:280,start:16}};
