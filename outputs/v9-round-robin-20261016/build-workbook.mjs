import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const dir=path.dirname(fileURLToPath(import.meta.url));
const data=JSON.parse(await fs.readFile(path.join(dir,'evaluation-data.json'),'utf8'));
if(!data.validation.passed || data.teamRows.length!==2240) throw new Error('Evaluation not verified');
const wb=Workbook.create();
const summary=wb.worksheets.add('종합순위');
const pairs=wb.worksheets.add('대진별결과');
const games=wb.worksheets.add('경기별지표');
const commands=wb.worksheets.add('명령별빈도');
const navy='#23364D', pale='#EFF3F7', ink='#172B40';
const end=data.teamRows.length+5;
const col=n=>{let s='';for(let x=n;x;x=Math.floor((x-1)/26))s=String.fromCharCode(65+(x-1)%26)+s;return s;};
function base(sh,lastCol,lastRow,title,context){
  const area=sh.getRange(`A1:${lastCol}${lastRow}`);
  area.format.font={name:'Arial',size:10,color:ink};
  area.format.verticalAlignment='center';area.format.rowHeight=22;area.format.columnWidth=12;
  sh.showGridLines=false;
  sh.getRange('A2').values=[[title]];sh.getRange('A2').format.font={name:'Arial',size:14,bold:true,color:ink};
  sh.getRange(`A2:${lastCol}2`).format.rowHeight=29;
  sh.getRange(`A3:${lastCol}3`).format.borders={bottom:{style:'thin',color:'#A9B6C5'}};
  sh.getRange('A3').values=[[context]];sh.getRange('A3').format.font={name:'Arial',size:10,italic:true,color:'#58677A'};
}
function header(sh,row,labels){
  const r=sh.getRange(`A${row}:${col(labels.length)}${row}`);r.values=[labels];
  r.format.fill=navy;r.format.font={name:'Arial',size:10,bold:true,color:'#FFFFFF'};
  r.format.wrapText=true;r.format.rowHeight=38;r.format.horizontalAlignment='center';
  r.format.borders={insideVertical:{style:'thin',color:'#FFFFFF'}};
}
function table(sh,range,name){const t=sh.tables.add(range,true,name);t.showFilterButton=true;}
function body(sh,range){sh.getRange(range).setNumberFormat('#,##0');sh.getRange(range).format.horizontalAlignment='right';}
function stripes(sh,start,finish,last){for(let r=start;r<=finish;r+=2)sh.getRange(`A${r}:${last}${r}`).format.fill=pale;}
const src="출처: evaluation-data.json / 원본 result.json·trace, 종료된 300초 경기만 집계";
base(games,'P',end,'경기별 지표 — 1,120경기 / 양 팀 2,240행',src);
header(games,5,['경기 ID','대진 ID','시드','진영','모델','상대 모델','승무패','득점','실점','슈팅 수','슛 득점','슈팅 성공률','패스 수','성공 패스','패스 성공률','감독 명령 수']);
games.getRange(`A6:P${end}`).values=data.teamRows.map(r=>[r.game,r.pair,r.seed,r.side,r.modelName,r.opponentName,r.result,r.goalsFor,r.goalsAgainst,r.shots,r.shotGoals,null,r.passAttempts,r.passSuccesses,null,r.decisions]);
games.getRange(`L6:L${end}`).formulas=data.teamRows.map((_,i)=>[`=IF(J${i+6}=0,"해당 없음",K${i+6}/J${i+6})`]);
games.getRange(`O6:O${end}`).formulas=data.teamRows.map((_,i)=>[`=IF(M${i+6}=0,"해당 없음",N${i+6}/M${i+6})`]);
body(games,`A6:C${end}`);body(games,`H6:P${end}`);
games.getRange(`L6:L${end}`).setNumberFormat('0.00%');games.getRange(`O6:O${end}`).setNumberFormat('0.00%');
games.getRange(`A1:D${end}`).format.columnWidth=10;games.getRange(`E1:F${end}`).format.columnWidth=13;
games.getRange(`G1:G${end}`).format.columnWidth=8;games.getRange(`L1:P${end}`).format.columnWidth=14;
table(games,`A5:P${end}`,'MatchMetrics');header(games,5,['경기 ID','대진 ID','시드','진영','모델','상대 모델','승무패','득점','실점','슈팅 수','슛 득점','슈팅 성공률','패스 수','성공 패스','패스 성공률','감독 명령 수']);
games.freezePanes.freezeRows(5);games.freezePanes.freezeColumns(6);

base(commands,'V',end,'감독 명령 — 선택 횟수와 전체 선택 중 비율',src);
const commandHeaders=['경기 ID','진영','모델','상대 모델','총 명령 수',...data.commandNames.map(x=>x+'\n횟수'),...data.commandNames.map(x=>x+'\n빈도 (%)'),'총 명령 / 분'];
commands.getRange(`A6:V${end}`).values=data.teamRows.map(r=>[r.game,r.side,r.modelName,r.opponentName,null,...r.commands,...Array(9).fill(null)]);
commands.getRange(`E6:E${end}`).formulas=data.teamRows.map((_,i)=>[`=SUM(F${i+6}:M${i+6})`]);
commands.getRange(`N6:U${end}`).formulas=data.teamRows.map((_,i)=>Array.from({length:8},(_,j)=>`=IF(E${i+6}=0,"해당 없음",${col(j+6)}${i+6}/E${i+6})`));
commands.getRange(`V6:V${end}`).formulas=data.teamRows.map((_,i)=>[`=E${i+6}/5`]);
body(commands,`E6:V${end}`);commands.getRange(`N6:U${end}`).setNumberFormat('0.00%');commands.getRange(`V6:V${end}`).setNumberFormat('0.00');
commands.getRange(`F1:U${end}`).format.columnWidth=19;commands.getRange(`V1:V${end}`).format.columnWidth=14;
header(commands,5,commandHeaders);table(commands,`A5:V${end}`,'CommandMetrics');header(commands,5,commandHeaders);commands.getRange('A5:V5').format.rowHeight=47;
commands.freezePanes.freezeRows(5);commands.freezePanes.freezeColumns(4);

const ref=c=>`'경기별지표'!$${c}$6:$${c}$${end}`;
const count=(row,outcome,opponent=false)=>`COUNTIFS(${ref('E')},${opponent?'B':'B'}${row}${opponent?`,${ref('F')},C${row}`:''}${outcome?`,${ref('G')},"${outcome}"`:''})`;
const sum=(c,row,opponent=false)=>`SUMIFS(${ref(c)},${ref('E')},B${row}${opponent?`,${ref('F')},C${row}`:''})`;
base(summary,'L',45,'GK 가속도 1.2배 — 8개 모델 전수 평가','1,120경기 · 모델당 280경기 · 조합당 40경기 · 300초 · 20개 시드 × 양 진영');
summary.tabColor=navy;
header(summary,5,['순위','모델','경기','승','무','패','승률','승점','득점','실점','득실차','경기당 승점']);
summary.getRange('B6:B13').values=data.ranking.map(r=>[r.model]);
summary.getRange('C6:L13').formulas=data.ranking.map((_,i)=>{const r=i+6;return [`=${count(r)}`,`=${count(r,'승')}`,`=${count(r,'무')}`,`=${count(r,'패')}`,`=D${r}/C${r}`,`=3*D${r}+E${r}`,`=${sum('H',r)}`,`=${sum('I',r)}`,`=I${r}-J${r}`,`=H${r}/C${r}`];});
summary.getRange('A6:A13').formulas=data.ranking.map((_,i)=>{const r=i+6;return [`=1+COUNTIF($H$6:$H$13,">"&H${r})+COUNTIFS($H$6:$H$13,H${r},$K$6:$K$13,">"&K${r})+COUNTIFS($H$6:$H$13,H${r},$K$6:$K$13,K${r},$I$6:$I$13,">"&I${r})`];});
body(summary,'C6:L13');summary.getRange('G6:G13').setNumberFormat('0.00%');summary.getRange('L6:L13').setNumberFormat('0.000');stripes(summary,6,13,'L');
summary.getRange('A15').values=[['승 3점 · 무 1점 · 패 0점 / 승점 → 득실차 → 득점 / 모두 같으면 공동 순위']];
header(summary,18,['순위','모델','슈팅 수','슛 득점','슈팅 성공률','패스 수','성공 패스','패스 성공률','명령 수','명령 / 경기','명령 / 분']);
summary.getRange('A19:B26').formulas=data.ranking.map((_,i)=>[`=A${i+6}`,`=B${i+6}`]);
summary.getRange('C19:K26').formulas=data.ranking.map((_,i)=>{const r=i+19;return [`=${sum('J',r)}`,`=${sum('K',r)}`,`=IF(C${r}=0,"해당 없음",D${r}/C${r})`,`=${sum('M',r)}`,`=${sum('N',r)}`,`=IF(F${r}=0,"해당 없음",G${r}/F${r})`,`=${sum('P',r)}`,`=I${r}/C${i+6}`,`=J${r}/5`];});
body(summary,'C19:K26');summary.getRange('E19:E26').setNumberFormat('0.00%');summary.getRange('H19:H26').setNumberFormat('0.00%');summary.getRange('J19:K26').setNumberFormat('0.00');stripes(summary,19,26,'K');
const u=data.uncertainty;const top=data.ranking[0],second=data.ranking[1];
const duel=data.pairSummaries.find(r=>r.id===top.id&&r.opponentId===second.id);
const ci=u.pointGap95CI.map(x=>x.toFixed(1)).join(' ~ ');
const notes=[
  `전체 1위: ${top.model} / 2위: ${second.model} / 승점차 ${u.observedPointGap}점`,
  `1위의 2위 상대 직접 대결: ${duel.wins}승 ${duel.draws}무 ${duel.losses}패 / 득실 ${duel.goalsFor}:${duel.goalsAgainst}`,
  `1·2위 승점차의 시드 묶음 부트스트랩 95% 구간: ${ci}점 (10,000회 재표집)`,
  `표본의 엄격한 승점 1위 재현 비율: ${(100*u.strictFirstPlaceFraction).toFixed(2)}% — 절대 우위의 확률이 아님`,
  '슈팅 성공률 = 슛에 귀속된 득점 / 실제 슈팅 수. 전체 득점에는 다른 원인의 득점이 포함될 수 있음.',
  '패스 성공률 = 지정 수신자의 수신 성공 / 실제 패스 시도. 시도 0회는 해당 없음.',
  '명령 빈도 (%) = 각 명령 선택 횟수 / 해당 팀 전체 선택 횟수. 총 명령 / 분 = 총 명령 수 / 5분.',
  'v8에서 학습한 동일 정책을 가속도 변경 환경 v9에서 평가. 이번 작업에서 재학습 및 champion 승격 없음.',
  '표준 킥오프와 정책 확률 샘플링 조건의 결과. 다른 초기 배치 또는 재학습 이후의 성능은 포함하지 않음.',
];
summary.getRange('A29:A37').values=notes.map(x=>[x]);summary.getRange('A29:A37').format.wrapText=false;
summary.getRange('A1:A45').format.columnWidth=9;summary.getRange('B1:B45').format.columnWidth=14;
summary.getRange('C1:K45').format.columnWidth=13;summary.getRange('L1:L45').format.columnWidth=16;
summary.getRange('K6:K13').conditionalFormats.add('cellIs',{operator:'lessThan',formula:0,format:{font:{color:'#AE302D'}}});

const pn=data.pairSummaries.length+5;
base(pairs,'R',pn,'대진별 결과 — 28개 조합 / 각 모델 관점 56행','각 행은 40경기. 같은 대진 ID의 두 행은 동일 경기들을 서로 반대 관점에서 표시.');
const pairHeaders=['대진 ID','모델','상대 모델','경기','승','무','패','득점','실점','득실차','승점','슈팅 수','슛 득점','슈팅 성공률','패스 수','성공 패스','패스 성공률','명령 수'];
pairs.getRange(`A6:C${pn}`).values=data.pairSummaries.map(r=>[r.pair,r.model,r.opponent]);
pairs.getRange(`D6:R${pn}`).formulas=data.pairSummaries.map((_,i)=>{const r=i+6;return [`=${count(r,null,true)}`,`=${count(r,'승',true)}`,`=${count(r,'무',true)}`,`=${count(r,'패',true)}`,`=${sum('H',r,true)}`,`=${sum('I',r,true)}`,`=H${r}-I${r}`,`=3*E${r}+F${r}`,`=${sum('J',r,true)}`,`=${sum('K',r,true)}`,`=IF(L${r}=0,"해당 없음",M${r}/L${r})`,`=${sum('M',r,true)}`,`=${sum('N',r,true)}`,`=IF(O${r}=0,"해당 없음",P${r}/O${r})`,`=${sum('P',r,true)}`];});
body(pairs,`D6:R${pn}`);pairs.getRange(`N6:N${pn}`).setNumberFormat('0.00%');pairs.getRange(`Q6:Q${pn}`).setNumberFormat('0.00%');
pairs.getRange(`B1:C${pn}`).format.columnWidth=13;pairs.getRange(`N1:R${pn}`).format.columnWidth=14;
header(pairs,5,pairHeaders);table(pairs,`A5:R${pn}`,'PairMetrics');header(pairs,5,pairHeaders);
pairs.freezePanes.freezeRows(5);pairs.freezePanes.freezeColumns(3);
wb.recalculate();
const ranked=summary.getRange('A6:L13').values;
for(let i=0;i<8;i++){
  const expected=data.ranking[i],actual=ranked[i];
  if(actual[0]!==expected.rank||actual[2]!==280||actual[7]!==expected.points||actual[8]!==expected.goalsFor||actual[9]!==expected.goalsAgainst)throw new Error('Summary mismatch '+JSON.stringify(actual));
}
const pairVals=pairs.getRange(`D6:R${pn}`).values;
for(let i=0;i<56;i++)if(pairVals[i][0]!==40||pairVals[i][7]!==data.pairSummaries[i].points)throw new Error('Pair mismatch '+i);
const errors=await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:30},maxChars:2500});
for(const sh of [summary,pairs,games,commands]){
  if(sh.getUsedRange().values.flat().some(v=>typeof v==='string'&&/^#(REF!|DIV\/0!|VALUE!|NAME\?|N\/A|NUM!|NULL!|SPILL!|CALC!)/.test(v)))throw new Error('Formula error in '+sh.name);
}
await fs.writeFile(path.join(dir,'workbook-inspection.json'),JSON.stringify({errors,ranked,pairRows:pairVals.length},null,2));
console.log(JSON.stringify({stage:'calculated',ranking:ranked,errors}));
for(const [sheetName,range,suffix] of [['종합순위','A1:L38','ranking'],['대진별결과','A1:R12','pairs'],['경기별지표','A1:P12','games'],['명령별빈도','A1:M11','commands-counts'],['명령별빈도','N5:V11','commands-rates']]){
  const image=await wb.render({sheetName,range,scale:1,format:'png'});
  await fs.writeFile(path.join(dir,`preview-${suffix}.png`),new Uint8Array(await image.arrayBuffer()));
}
const xlsx=await SpreadsheetFile.exportXlsx(wb);
await xlsx.save(path.join(dir,'골키퍼가속도12배_8모델_1120경기.xlsx'));
await fs.writeFile(path.join(dir,'workbook-verification.json'),JSON.stringify({passed:true,games:1120,teamRows:2240,pairRows:56,rankingRows:8,formulasChecked:true,rankingMatchesEvidence:true},null,2));
console.log('WORKBOOK COMPLETE');
