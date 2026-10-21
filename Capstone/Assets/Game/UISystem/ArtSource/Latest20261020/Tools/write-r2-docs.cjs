const fs=require('fs'),path=require('path');
module.exports=async(H)=>{const templates=JSON.parse(fs.readFileSync(path.join(__dirname,'document-templates.json'),'utf8'));for(const [file,text] of Object.entries(templates))H.put(file,text);};
