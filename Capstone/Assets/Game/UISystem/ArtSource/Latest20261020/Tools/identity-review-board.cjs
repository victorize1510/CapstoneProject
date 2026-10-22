const path=require('path');
module.exports=async function(H,O){
 const {root,C,sharp,doc,txt,img,source}=H,t=(s,x,y,n=20)=>txt(s,x,y,n,C.light,650);
 const pic=async(f,x,y,w,h)=>img((await sharp(path.join(root,f)).resize(w,h).png().toBuffer()).toString('base64'),x,y,w,h);
 let b='<rect width="1600" height="1460" fill="#28334C"/>'+t('AVATAR CỐ ĐỊNH · BA TAB LIỀN KHUNG · SKILL VUÔNG VÁT 8 CẠNH',28,52,27)+t('Tinh chỉnh 20/10/2026 · Khung avatar không chứa icon hoặc thao tác đổi ảnh',28,88,19);
 for(let j=0;j<2;j++){const buf=await sharp(path.join(root,j?'Screens/01_Menu.png':'Reference/Before_2026-10-20/01_Menu.png')).extract({left:768,top:52,width:776,height:116}).png().toBuffer();const y=146+j*170;b+=t(j?'SAU: KHUNG CHỨA AVATAR, THÔNG TIN CÓ NÉT KHẮC':'TRƯỚC: ICON ĐẠI DIỆN',28,y-14,17)+img(buf.toString('base64'),28,y,776,116);}
 for(let yy=0;yy<11;yy++)for(let xx=0;xx<11;xx++)b+='<rect x="'+(890+xx*24)+'" y="'+(142+yy*24)+'" width="24" height="24" fill="'+((xx+yy)%2?'#647189':'#98A3B7')+'"/>';
 b+=await pic('Assets/Identity/avatar-frame.png',890,142,264,264)+t('Chỉ viền; tâm alpha thật',1190,202,19)+t('88×88, cửa ảnh an toàn 64×64',1190,241,17)+t('Không nút edit, picker hoặc hover',1190,280,17)+t('Ảnh do provider hiện có cung cấp',1190,319,17);
 b+=t('CẢ BA TAB CÓ CHÂN CHUNG, KHÔNG CÒN KHOẢNG HỞ DƯỚI TAB CHƯA CHỌN',28,535,22);
 for(let i=0;i<3;i++){const x=28+i*526,f=['02_Tong_Quan','03_Ky_Nang','04_Phat_Trien'][i];const crop=await sharp(path.join(root,'Screens/'+f+'.png')).extract({left:704,top:96,width:864,height:240}).resize(492,137).png().toBuffer();b+=t(['TỔNG QUAN','KỸ NĂNG','PHÁT TRIỂN'][i],x,580,18)+img(crop.toString('base64'),x,600,492,137);}
 b+=t('MỘT ĐƯỜNG BAO CHO CẢ TAB VÀ PANEL · VÂN CHẠY LIÊN TỤC QUA CHÂN TAB',28,798,20);
 for(let i=0;i<3;i++){const f=await sharp(path.join(root,'Frames/inspector-'+i+'.png')).extract({left:0,top:0,width:864,height:94}).resize(492,54).png().toBuffer();b+=img(f.toString('base64'),28+i*526,820,492,54);}
 b+=t('SKILL GIỮ DÁNG VUÔNG, CHỈ VÁT BỐN GÓC · GIỮ SPRITE VÀ MÀU GỐC',28,955,22);
 for(let i=0;i<3;i++)b+=H.skill(28+i*150,989,106,['bite','blast','missing'][i]);
 b+=t('Khung kim loại mảnh, nét khắc nhỏ ở hai góc',530,1020,20)+t('Icon và artwork cắt theo cùng mask tám cạnh',530,1060,20)+t('Không biến thành bát giác đều hoặc hình thoi',530,1100,20);
 const states=['normal','hover','pressed','focus','selected-focus','disabled'];for(let i=0;i<6;i++){const x=28+i*260;b+=await pic('States/skill-'+states[i]+'.png',x,1182,118,118)+t(states[i],x,1335,17);}
 b+=t('Concept/asset thiết kế; không sửa Unity. Giữ CRIT RATE / CRIT DMG và mặc định 2D.',28,1425,18);
 source('Boards/06_Avatar_Tab_Skill.svg',doc(1600,1460,b));await sharp(Buffer.from(doc(1600,1460,b))).png().toFile(path.join(root,'Boards/06_Avatar_Tab_Skill.png'));
};
