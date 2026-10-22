// Reusable surface engraving. The caller clips to the canonical contour.
let seq=0;
const SETTINGS={shell:{engraving:.19,material:.13},header:{engraving:.18,material:.12},content:{engraving:.105,material:.10},button:{engraving:.27,material:.14}};
function draw(w,h,density='content',tone='#66748E'){
 if(w<110||h<40)return '';
 const id='terrain'+(++seq),role=SETTINGS[density]?density:'content',button=role==='button',header=role==='header',shell=role==='shell';
 const light=parseInt(tone.slice(1,3),16)*.2126+parseInt(tone.slice(3,5),16)*.7152+parseInt(tone.slice(5,7),16)*.0722>145;
 const opacity=SETTINGS[role].engraving*(light?.82:1),metal=light?'#4C5D7C':'#D7C3A0',shadow=light?'#EFF0F2':'#202D48';
 let body='',facets='';
 if(button||header){
  const edge=Math.min(66,w*.24),r=Math.min(h*.41,32),cx=w-h*.50-5,cy=h*.50;
  for(let k=0;k<3;k++){const yy=h*.16+k*5;body+='<path d="M'+(12+k*3)+','+(h*.69+k*3)+'Q'+(edge*.56)+','+yy+' '+(edge+20)+','+(7+k*3)+'"/>';}
  body+='<circle cx="'+cx+'" cy="'+cy+'" r="'+r+'"/><circle cx="'+cx+'" cy="'+cy+'" r="'+(r*.74)+'"/>';
  body+='<path d="M'+cx+','+(cy-r*.92)+'l'+(r*.12)+','+(r*.80)+' '+(r*.80)+','+(r*.12)+'-'+(r*.80)+','+(r*.12)+'-'+(r*.12)+','+(r*.80)+'-'+(r*.12)+'-'+(r*.80)+'-'+(r*.80)+'-'+(r*.12)+' '+(r*.80)+'-'+(r*.12)+'Z"/>';
  for(let k=0;k<3;k++){const x=w-16-k*6;body+='<path d="M'+x+','+(h-9)+'l-8,-5"/>';}
  body+='<path d="M'+(edge*.48)+','+(h-9)+'h'+Math.min(52,w*.15)+'m5,0h10"/>';
  facets='<path d="M0,0H'+(w*.31)+'L'+(w*.21)+','+h+'H0Z" fill="#DEE5EE" opacity=".065"/><path d="M'+(w*.76)+',0H'+w+'V'+h+'H'+(w*.67)+'Z" fill="#172A48" opacity=".075"/><path d="M0,'+(h*.68)+'Q'+(w*.38)+','+(h*.85)+' '+w+','+(h*.55)+'V'+(h*.68)+'Q'+(w*.4)+','+(h*.96)+' 0,'+(h*.80)+'Z" fill="#D1CCBE" opacity=".045"/>';
 }else{
  const left=Math.min(w*.23,160);
  for(let k=0;k<6;k++){const z=k*8;body+='<path d="M'+(8+z)+',0C'+(left+z)+','+(h*.22)+' '+(12-z)+','+(h*.40)+' '+(left*.64+z)+','+(h*.67)+'S'+(left*1.35+z)+','+(h*.84)+' '+(left*.56+z)+','+h+'"/>';}
  const r=Math.min(h*.27,w*.16,94),cx=w-Math.max(28,w*.085),cy=shell?h*.22:h*.34;
  for(const f of [.59,.82,1])body+='<circle cx="'+cx+'" cy="'+cy+'" r="'+(r*f)+'"/>';
  for(let k=0;k<12;k++){const a=k*Math.PI/6;body+='<path d="M'+(cx+Math.cos(a)*r*.84)+','+(cy+Math.sin(a)*r*.84)+'L'+(cx+Math.cos(a)*r*1.05)+','+(cy+Math.sin(a)*r*1.05)+'"/>';}
  body+='<path d="M'+cx+','+(cy-r*.84)+'L'+(cx+r*.13)+','+cy+'L'+cx+','+(cy+r*.84)+'L'+(cx-r*.13)+','+cy+'Z"/>';
  const count=shell?5:3,step=Math.min(22,w*.08);
  for(let k=0;k<count;k++){const x=24+k*step,y=h-19-(k%2)*3;body+='<path d="M'+x+','+y+'v-11l6,-4 6,4v11m-12,-5h12m-6,-10v20"/>';}
  for(let k=0;k<3;k++)body+='<path d="M'+(w*.55+k*7)+','+(h-8)+'C'+(w*.72)+','+(h*.73+k*3)+' '+(w*.89)+','+(h*.99)+' '+(w-9)+','+(h*.50+k*12)+'"/>';
  facets='<path d="M0,0H'+(w*.42)+'L'+(w*.16)+','+(h*.30)+'H0Z" fill="#DEE5EE" opacity=".035"/><path d="M'+(w*.64)+','+h+'H'+w+'V'+(h*.67)+'Z" fill="#233550" opacity=".035"/>';
 }
 const gradient=button||header?
 '<linearGradient id="'+id+'"><stop stop-color="white"/><stop offset=".22" stop-color="white"/><stop offset=".38" stop-color="black"/><stop offset=".68" stop-color="black"/><stop offset=".83" stop-color="white"/><stop offset="1" stop-color="white"/></linearGradient>':
 '<radialGradient id="'+id+'" cx="46%" cy="47%" r="66%"><stop stop-color="black"/><stop offset=".40" stop-color="black"/><stop offset=".76" stop-color="white" stop-opacity=".76"/><stop offset="1" stop-color="white"/></radialGradient>';
 return '<defs>'+gradient+'<mask id="'+id+'mask"><rect width="'+w+'" height="'+h+'" fill="url(#'+id+')"/></mask></defs><g data-surface-pattern="'+role+'-engraving">'+facets+'<g data-panel-motif="cartography-and-runes" opacity="'+opacity+'" mask="url(#'+id+'mask)"><g fill="none" stroke="'+metal+'" stroke-width="'+(shell?1.35:button?1.1:1.15)+'" stroke-linejoin="round" stroke-linecap="round">'+body+'</g><g transform="translate(0 .8)" fill="none" stroke="'+shadow+'" stroke-width=".65" opacity=".6">'+body+'</g></g></g>';
}
module.exports={draw,SETTINGS};
