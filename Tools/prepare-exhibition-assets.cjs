// Reproducible import of approved design assets; source files are never modified.
const fs=require('fs'), path=require('path'), crypto=require('crypto');
const sharp=require('C:/Users/USER/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const src=path.resolve(__dirname,'../docs/archive/exhibition/ui-v1-final-20260927');
const dst=path.resolve(__dirname,'../Assets/_Soccer/Manager/Exhibition');
async function main(){
  for(const dir of ['Art','Fonts','Models','Data','UI','Runtime','Editor']) fs.mkdirSync(path.join(dst,dir),{recursive:true});
  for(const name of fs.readdirSync(path.join(src,'assets'))){
    if(!name.endsWith('.svg')||name==='pitch.svg')continue;
    await sharp(path.join(src,'assets',name)).resize({width:1920}).png().toFile(path.join(dst,'Art',name.replace('.svg','.png')));
  }
  for(const name of fs.readdirSync(path.join(src,'assets/fonts'))) fs.copyFileSync(path.join(src,'assets/fonts',name),path.join(dst,'Fonts',name));
  const catalog=JSON.parse(fs.readFileSync(path.join(src,'model-catalog.json'),'utf8'));
  for(const m of catalog.models.filter(x=>x.kind==='neural')){
    const file=path.resolve(__dirname,'..',m.source), data=fs.readFileSync(file);
    if(crypto.createHash('sha256').update(data).digest('hex')!==m.sha256) throw new Error('Model hash mismatch: '+m.id);
    fs.writeFileSync(path.join(dst,'Models',m.id+'.onnx'),data);
  }
  fs.copyFileSync(path.join(src,'model-catalog.json'),path.join(dst,'Data/model-catalog.json'));
  // Parse RFC4180 quoted fields in the approved localization CSV.
  const rows=[];let row=[],cell='',quoted=false;
  const csv=fs.readFileSync(path.join(src,'ui-strings.csv'),'utf8');
  for(let i=0;i<csv.length;i++){const c=csv[i];if(c==='"'){if(quoted&&csv[i+1]==='"'){cell+='"';i++;}else quoted=!quoted;}else if(c===','&&!quoted){row.push(cell);cell='';}else if(c==='\n'&&!quoted){row.push(cell.replace(/\r$/,''));rows.push(row);row=[];cell='';}else cell+=c;}
  if(cell){row.push(cell);rows.push(row);}
  const entries=rows.slice(1).map(r=>({key:r[0],ko:r[1],en:r[2]}));
  entries.find(e=>e.key==='MNG.Exhibition.aiSwitch').en='Switch to AI';
  entries.push({key:'MNG.Exhibition.humanSwitch',ko:'사람 모드로 전환',en:'Switch to human control'});
  for(const m of catalog.models){entries.push({key:'model.'+m.id,ko:m.label.ko,en:m.label.en});entries.push({key:'selection.'+m.id,ko:m.selectionLabel?.ko??(m.kind==='rule'?m.label.en+': '+m.label.ko:m.label.ko),en:m.selectionLabel?.en??m.label.en});if(m.description)entries.push({key:'description.'+m.id,...m.description});}
  fs.writeFileSync(path.join(dst,'Data/strings.json'),JSON.stringify({entries},null,2));
  console.log('Approved asset import complete: '+entries.length+' bilingual entries, 6 SHA-verified models.');
}
main().catch(e=>{console.error(e);process.exit(1)});
