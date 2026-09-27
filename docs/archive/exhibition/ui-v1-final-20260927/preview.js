(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const all = selector => [...document.querySelectorAll(selector)];
  const state = {screen:'splash', locale:'ko', minutes:5, human:true, humanActive:true, red:'stage-2000000', navy:'stage-2000000', modelTeam:'red', modelTab:'steps', guidePage:0, seconds:300, redScore:0, navyScore:0, aiShots:[0,0], humanShots:[0,0], liveTick:-1, rewards:[0,0], recoveries:[0,0], modal:null};
  const names = {splash:'팀 로고 · 자동으로 페이드 아웃',home:'시작 화면 · MANAGER는 임시 게임 제목',setup:'경기 설정 · 양 팀 독립 선택 / 400K 간격',match:'시안 · 판단·보상은 3초마다 바뀌는 예시 / 실제 추론 아님',result:'예시 기록 · 전체 슈팅 = AI + 사람 / 선방 — 는 향후 관측 연결'};
  let splashTimer, fadeTimer, matchTimer, lastTick, goalTimer, goalDeadline=0, goalRemaining=0, returnFocus;
  let paused=false;
  const t = key => window.MNG_I18N[key]?.[state.locale] ?? key;
  const model = id => window.MNG_CATALOG.models.find(item => item.id === id);
  const kind = item => t(item.kind === 'neural' ? 'neural' : 'rule');
  const label = item => item.label[state.locale];
  function text(id,value){ $(id).textContent=value; }
  function time(value){const seconds=Math.max(0,Math.ceil(value)); return `${String(Math.floor(seconds/60)).padStart(2,'0')}:${String(seconds%60).padStart(2,'0')}`;}
  function reward(value){return `${value>=0?'+':''}${value.toFixed(3)}`;}
  function closeLanguage(focus=false){$('languageMenu').hidden=true;$('language').setAttribute('aria-expanded','false');if(focus)$('language').focus();}
  function closeModal(){if(!state.modal)return;$(state.modal).hidden=true;state.modal=null;all('.screen').forEach(el=>el.inert=false);returnFocus?.focus();}
  function openModal(id){closeLanguage();returnFocus=document.activeElement;state.modal=id;$(id).hidden=false;all('.screen').forEach(el=>el.inert=true);$(id).querySelector('button:not(:disabled)')?.focus();}
  function pauseMatch(){
    if(state.screen!=='match'||paused||state.modal)return;
    paused=true;clearTimeout(goalTimer);goalRemaining=$('goalBanner').hidden?0:Math.max(0,goalDeadline-performance.now());
    for(const id of ['sampleGoal','sampleNavyGoal','sampleEnd'])$(id).disabled=true;
    openModal('pauseModal');
  }
  function resumeMatch(){
    if(!paused)return;
    closeModal();paused=false;lastTick=performance.now();
    if(goalRemaining>0){goalDeadline=lastTick+goalRemaining;goalTimer=setTimeout(()=>{$('goalBanner').hidden=true;},goalRemaining);}else{$('goalBanner').hidden=true;}
    for(const id of ['sampleGoal','sampleNavyGoal','sampleEnd'])$(id).disabled=false;
    $('game').focus({preventScroll:true});
  }
  function applyLocale(){
    document.documentElement.lang=state.locale;
    all('[data-i18n]').forEach(el=>{el.innerHTML=t(el.dataset.i18n);});
    all('[data-locale]').forEach(el=>{const selected=el.dataset.locale===state.locale;el.setAttribute('aria-checked',String(selected));el.querySelector('span').textContent=selected?'✓':'';});
    all('.close').forEach(el=>el.setAttribute('aria-label',t('close')));
    document.querySelector('.pitch').setAttribute('alt',t('pitchAlt'));
    $('creditsImage').src=`assets/credits-${state.locale}.svg`;
    $('creditsImage').alt=t('creditsAlt');
    updateModels(); updateMode(); updateGuide(); renderResults();
    if(state.modal==='modelModal')renderModelList();
  }
  function show(screen){
    clearTimeout(splashTimer);clearTimeout(fadeTimer);clearInterval(matchTimer);clearTimeout(goalTimer);closeModal();closeLanguage();
    state.screen=screen;paused=false;goalRemaining=0;
    all('.screen').forEach(el=>el.hidden=el.id!==screen);
    all('[data-preview]').forEach(el=>el.classList.toggle('active',el.dataset.preview===screen));
    text('reviewStatus',names[screen]);$('sampleGoal').disabled=screen!=='match';$('sampleNavyGoal').disabled=screen!=='match';$('sampleEnd').disabled=screen!=='match';
    $('goalBanner').hidden=true;
    if(screen==='splash'){
      $('splash').classList.remove('fading');
      splashTimer=setTimeout(()=>{$('splash').classList.add('fading');fadeTimer=setTimeout(()=>show('home'),700);},1500);
    }
    if(screen==='home')$('start').focus({preventScroll:true});
    if(screen==='setup')$('redPick').focus({preventScroll:true});
    if(screen==='match'){
      text('reviewStatus',`시안 · ${state.human?'직접 플레이 1배속':'AI 시뮬레이션 2배속'} · 판단·보상은 경기 시간 3초마다 바뀌는 예시`);
      state.humanActive=state.human;updateMode();state.seconds=state.minutes*60;state.redScore=0;state.navyScore=0;state.aiShots=[0,0];state.humanShots=[0,0];state.liveTick=-1;state.rewards=[0,0];state.recoveries=[0,0];updateHud();updateModels();updateLive();lastTick=performance.now();
      matchTimer=setInterval(()=>{if(paused)return;const now=performance.now();const speed=state.human?1:2;state.seconds=Math.max(0,state.seconds-(now-lastTick)/1000*speed);lastTick=now;updateHud();updateLive();if(state.seconds<=0){renderResults();show('result');}},200);
      $('game').focus({preventScroll:true});
    }
    if(screen==='result'){renderResults();$('game').focus({preventScroll:true});}
  }
  function updateModels(){
    for(const team of ['red','navy']){const item=model(state[team]);text(`${team}Model`,label(item));text(`${team}Kind`,item.group==='steps'?`MS3-v3 · ${kind(item)}`:t('referenceTab'));text(`live${team==='red'?'Red':'Navy'}Model`,`${kind(item)} · ${label(item)}`);}
  }
  function updateMode(){
    $('speedBadge').hidden=state.human;
    $('humanMode').setAttribute('aria-pressed',String(state.human));$('simMode').setAttribute('aria-pressed',String(!state.human));
    text('playerBadge',t(state.human?'yourTeam':'aiTeam'));$('keyHints').hidden=!state.human;
    all('[data-minutes]').forEach(el=>el.setAttribute('aria-pressed',String(Number(el.dataset.minutes)===state.minutes)));
  }
  function updateGuide(){
    $('guideImage').src=`assets/guide-${state.locale}-${state.guidePage+1}.svg`;
    $('guideImage').alt=`${t('guide')} ${state.guidePage+1}: ${t(`guideAlt${state.guidePage+1}`)}`;
    text('guidePage',`0${state.guidePage+1} / 03`);
    $('guideNext').innerHTML=`<kbd>ENTER</kbd> <span>${t(state.guidePage===2?'finishGuide':'next')}</span>`;
  }
  function nextGuide(){if(state.guidePage===2){closeModal();return;}state.guidePage++;updateGuide();}
  function renderModelList(){
    text('modelTitle',`${state.modelTeam.toUpperCase()} · ${t('selectModel')}`);
    all('[data-model-tab]').forEach(el=>el.setAttribute('aria-selected',String(el.dataset.modelTab===state.modelTab)));
    text('modelNotice',t(state.modelTab==='steps'?'stepsNotice':'referenceNotice'));
    const items=window.MNG_CATALOG.models.filter(item=>item.group===state.modelTab);
    $('modelList').dataset.group=state.modelTab;
    $('modelList').replaceChildren();
    for(const item of items){
      const button=document.createElement('button');button.className='model-option';button.setAttribute('aria-pressed',String(state[state.modelTeam]===item.id));
      const title=document.createElement('strong');title.textContent=item.kind==='rule'?item.label.en:(item.selectionLabel?.[state.locale]??label(item));
      if(item.kind==='rule'){title.lang='en';if(state.locale==='ko'){title.append(document.createTextNode(': '));const korean=document.createElement('span');korean.className='rule-korean';korean.lang='ko';korean.textContent=item.label.ko;title.append(korean);}}
      const sub=document.createElement('small');sub.textContent=item.kind==='neural'?`${kind(item)} · ${Number(item.actualStep).toLocaleString(state.locale==='ko'?'ko-KR':'en-US')} ${item.actualStep===1?'step':'steps'}`:item.description[state.locale];
      button.append(title,sub);button.addEventListener('click',()=>{state[state.modelTeam]=item.id;updateModels();closeModal();});$('modelList').append(button);
    }
  }
  function openModels(team){state.modelTeam=team;state.modelTab=model(state[team]).group;renderModelList();openModal('modelModal');}
  function updateHud(){text('clock',time(state.seconds));text('hudRedScore',state.redScore);text('hudNavyScore',state.navyScore);}
  function updateLive(){
    // Illustrative UI animation only. This preview has no model inference or match telemetry.
    const tick=Math.floor((state.minutes*60-state.seconds)/3);
    if(tick===state.liveTick)return;
    state.liveTick=tick;
    for(const [index,team] of ['red','navy'].entries()){
      const item=model(state[team]);
      const patterns={recover:[3],balanced:[4],'carry-shot':[0,2,3],'uniform-valid':[4,5,3,4,3,5]};
      const pattern=patterns[item.source]??(index===0?[0,1,2,3,4,5]:[3,4,5,0,1,2]);
      text(`${team}Decision`,t(`command${pattern[tick%pattern.length]}`));
      if(tick>0)state.rewards[index]+=[.035,-.01,.065,.02][(tick+index)%4];
      text(`${team}Reward`,reward(state.rewards[index]));
    }
  }
  function renderResults(){
    text('winner',state.redScore===state.navyScore?t('draw'):`${state.redScore>state.navyScore?'RED':'NAVY'} ${t('wins')}`);
    const shots=state.aiShots.map((value,index)=>value+state.humanShots[index]);
    text('resultRed',state.redScore);text('resultNavy',state.navyScore);text('shotsRed',shots[0]);text('shotsNavy',shots[1]);
    for(const [index,team] of ['Red','Navy'].entries()){text(`aiShots${team}`,state.aiShots[index]);text(`humanShots${team}`,state.humanShots[index]);text(`resultReward${team}`,reward(state.rewards[index]));text(`recoveries${team}`,state.recoveries[index]);text(`saves${team}`,'—');}
    text('resultRedModel',label(model(state.red)));text('resultNavyModel',label(model(state.navy)));text('resultDuration',t(state.minutes===5?'five':'ten'));
    const total=shots[0]+shots[1];$('redShotBar').style.width=`${total?shots[0]/total*100:50}%`;
  }
  function sampleResults(){state.redScore=2;state.navyScore=1;state.aiShots=[state.human?6:8,6];state.humanShots=[state.human?2:0,0];state.rewards=[2.450,1.870];state.recoveries=[12,9];state.seconds=0;show('result');}
  function sampleGoal(team){
    if(paused)return;
    const index=team==='red'?0:1;
    state[`${team}Score`]++;
    (index===0&&state.human&&state.humanActive?state.humanShots:state.aiShots)[index]++;
    updateHud();text('goalTeam',team.toUpperCase());$('goalBanner').dataset.team=team;$('goalBanner').hidden=false;
    clearTimeout(goalTimer);goalDeadline=performance.now()+2400;goalTimer=setTimeout(()=>{$('goalBanner').hidden=true;},2400);
  }
  $('start').onclick=()=>show('setup');$('setupBack').onclick=()=>show('home');$('exitResult').onclick=()=>show('home');$('kickoff').onclick=()=>show('match');
  $('resumeMatch').onclick=resumeMatch;$('quitMatch').onclick=()=>show('home');
  $('language').onclick=()=>{const open=$('languageMenu').hidden;$('languageMenu').hidden=!open;$('language').setAttribute('aria-expanded',String(open));if(open)$('languageMenu').querySelector('[aria-checked="true"]').focus();};
  all('[data-locale]').forEach(el=>el.onclick=()=>{state.locale=el.dataset.locale;applyLocale();closeLanguage(true);});
  $('guideOpen').onclick=()=>{state.guidePage=0;updateGuide();openModal('guideModal');};$('guideNext').onclick=nextGuide;
  $('creditsOpen').onclick=()=>openModal('creditsModal');
  all('[data-close]').forEach(el=>el.onclick=closeModal);
  $('redPick').onclick=()=>openModels('red');$('navyPick').onclick=()=>openModels('navy');
  all('[data-model-tab]').forEach(el=>el.onclick=()=>{state.modelTab=el.dataset.modelTab;renderModelList();});
  $('humanMode').onclick=()=>{state.human=true;updateMode();};$('simMode').onclick=()=>{state.human=false;updateMode();};
  all('[data-minutes]').forEach(el=>el.onclick=()=>{state.minutes=Number(el.dataset.minutes);updateMode();});
  all('[data-preview]').forEach(el=>el.onclick=()=>{if(el.dataset.preview==='result'){sampleResults();return;}show(el.dataset.preview);});
  $('sampleGoal').onclick=()=>sampleGoal('red');$('sampleNavyGoal').onclick=()=>sampleGoal('navy');
  $('sampleEnd').onclick=sampleResults;
  document.addEventListener('click',event=>{if(!event.target.closest('.language-anchor'))closeLanguage();});
  document.addEventListener('keydown',event=>{
    if(event.key==='Tab'&&state.modal){const controls=[...$(state.modal).querySelectorAll('button:not(:disabled)')].filter(el=>!el.hidden);const first=controls[0],last=controls[controls.length-1];if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}return;}
    if(event.key==='Escape'){
      if(event.repeat)return;
      if(state.modal==='pauseModal'){event.preventDefault();resumeMatch();return;}
      if(state.modal){event.preventDefault();closeModal();return;}
      if(!$('languageMenu').hidden){event.preventDefault();closeLanguage(true);return;}
      if(state.screen==='match'){event.preventDefault();pauseMatch();return;}
      if(state.screen==='setup'){event.preventDefault();show('home');}return;
    }
    if(event.key==='Enter'&&state.modal==='guideModal'&&!event.repeat){event.preventDefault();nextGuide();return;}
    if(state.screen==='match'&&!paused&&state.human&&event.key.toLowerCase()==='h'&&!event.repeat){state.humanActive=!state.humanActive;updateMode();}
    if(!$('languageMenu').hidden&&['ArrowDown','ArrowUp'].includes(event.key)){event.preventDefault();const buttons=[...$('languageMenu').querySelectorAll('button:not(:disabled)')];const index=buttons.indexOf(document.activeElement);buttons[(index+(event.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length].focus();}
  });
  applyLocale();show('splash');
})();
