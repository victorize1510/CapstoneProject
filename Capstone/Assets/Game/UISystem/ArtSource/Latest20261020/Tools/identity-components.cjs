// Exact vector components; no portrait art or avatar editing affordance.
const G=require('./geometry.cjs'),O=require('./integrated-frame.cjs'),M=require('./panel-motifs.cjs');
let H,seq=0;
function configure(host){H=host;}
function avatarFrame(x,y,n=88){
 const {C,clipped}=H,poly=G.pts('avatar',n,n),p=G.pathOf(poly),r=n/88,ring='avatarRing'+(++seq);
 const art=O.trim(poly,n,n,'light')+'<g fill="none" stroke="'+C.gold+'" stroke-width="1.15" opacity=".82"><path d="M'+16*r+' '+7*r+'H'+34*r+'M'+(n-34*r)+' '+(n-7*r)+'H'+(n-16*r)+'"/><path d="M'+7*r+' '+(n-34*r)+'v'+18*r+'l'+9*r+','+9*r+'M'+(n-7*r)+' '+34*r+'v-'+18*r+'l-'+9*r+',-'+9*r+'"/></g><path d="M'+n/2+' '+4*r+'l'+3*r+','+3*r+'-'+3*r+','+3*r+'-'+3*r+',-'+3*r+'Z" fill="'+C.gold+'" opacity=".85"/>';
 return '<g data-component="fixed-player-avatar-frame" data-interactive="false" transform="translate('+x+' '+y+')">'+clipped(p,n,n,'<defs><mask id="'+ring+'"><rect width="'+n+'" height="'+n+'" fill="white"/><path d="'+G.pathOf(G.pts('avatar',n,n,12*r))+'" fill="black"/></mask></defs><g mask="url(#'+ring+')">'+art+'</g>')+'</g>';
}
function playerInfoFace(w=776,h=116){
 const {C,fill,clipped}=H,p=G.pathOf(G.pts('panel',w,h)),cx=w-52,cy=h/2,r=38;
 const etch='<g data-identity-engraving="expedition-registry" fill="none" stroke="'+C.gold+'" stroke-width="1" opacity=".26"><path d="M118 16H302l10-5h46M124 '+(h-10)+'H578l12-5h54"/><path d="M117 25v64l8 9"/><circle cx="'+cx+'" cy="'+cy+'" r="'+r+'"/><circle cx="'+cx+'" cy="'+cy+'" r="'+r*.73+'"/><path d="M'+(cx-r)+' '+cy+'h'+2*r+'M'+cx+' '+(cy-r)+'v'+2*r+'"/><path d="M'+cx+' '+(cy-r*.82)+'l5 '+r*.69+' '+r*.69+' 5-'+r*.69+' 5-5 '+r*.69+'-5-'+r*.69+'-'+r*.69+'-5 '+r*.69+'-5Z"/></g><path d="M'+(w-122)+' 9h42l-56 '+(h-18)+'h-42Z" fill="'+C.primary+'" opacity=".04"/>';
 return fill(p,w,h,C.panel,.96)+clipped(p,w,h,etch);
}
function skillFrame(n=72,selected=false){
 const {C,border,clipped}=H,poly=G.pts('skill-oct',n,n),p=G.pathOf(poly);
 return O.trim(poly,n,n,'slot')+(selected?border(p,n,n,C.gold,true):'')+clipped(p,n,n,'<path d="M15 7h10M'+(n-15)+' '+(n-7)+'h-10" fill="none" stroke="'+C.gold+'" stroke-width=".9" opacity=".58"/>');
}
function skill(x,y,n=72,id='missing',selected=false){
 const {C,A,img,icon,fill,clipped}=H,data=id==='bite'?A.bite:id==='blast'?A.blast:id==='potion'?A.potion:null,p=G.pathOf(G.pts('skill-oct',n,n)),glyph=data?img(data,n*.12,n*.12,n*.76,n*.76):icon('skills',n*.28,n*.28,n*.44,C.muted);
 return '<g data-component="octagonal-square-skill" transform="translate('+x+' '+y+')">'+fill(p,n,n,C.primary,id==='bite'?.80:.26)+clipped(p,n,n,glyph)+skillFrame(n,selected)+'</g>';
}
function tabDeck(i){
 const {C,clipped}=H,w=864,h=620,p=G.pathOf(G.unionPoly(i)),k='deck'+(++seq),active=G.pathOf(G.tabCapPoly(i,i));
 let body='<defs><mask id="'+k+'"><rect width="864" height="620" fill="white"/><path d="'+active+'" fill="black"/></mask><linearGradient id="'+k+'metal" x2="0" y2="1"><stop stop-color="'+C.shell+'"/><stop offset="1" stop-color="'+C.primary+'" stop-opacity=".42"/></linearGradient></defs><rect x="4" y="48" width="856" height="20" fill="'+C.shell+'" opacity=".48" mask="url(#'+k+')"/>';
 for(let j=0;j<3;j++){
  const x=16+j*280,cap=G.pathOf(G.tabCapPoly(j,i)),pattern='<g transform="translate('+x+' 8)">'+M.draw(272,64,'button',j===i?C.panel:C.shell)+'</g>';
  if(j!==i)body+='<path d="'+cap+'" fill="url(#'+k+'metal)" fill-opacity=".96"/>';
  body+=clipped(cap,w,h,pattern);
 }
 body+='<g fill="none" stroke="'+C.gold+'" stroke-width=".8" opacity=".30"><path d="M292 56v10m280-10v10"/></g>';
 return clipped(p,w,h,body);
}
function inspectorFace(i){const p=G.pathOf(G.unionPoly(i)),etch='<g data-inspector-engraving="bond-and-survey" fill="none" stroke="'+H.C.gold+'" stroke-width="1" opacity=".18"><path d="M14 83v49l7 8M850 84v62l-6 7M24 150h298l11-5h69M28 610h246l13-5h78"/><path d="M620 147l3-3 3 3-3 3Zm15 0l3-3 3 3-3 3Zm15 0l3-3 3 3-3 3Z"/></g>';return H.fill(p,864,620,H.C.panel,.96)+tabDeck(i)+H.clipped(p,864,620,etch);}
module.exports={configure,avatarFrame,playerInfoFace,skillFrame,skill,tabDeck,inspectorFace};
