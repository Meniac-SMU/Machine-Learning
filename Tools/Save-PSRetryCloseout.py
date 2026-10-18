import json,hashlib,re
from pathlib import Path
root=Path('C:/GitHub/Machine-Learning');e=root/'Logs/MNG-Rebuild/PS-Retry-20260929'
completion=json.loads((e/'train-to-600000/completion.json').read_text());assert completion['status']=='saved-stopped'
rows=[json.loads(p.read_text()) for p in sorted((e/'duel-500k').glob('match-*/verified.json'))]
partial={k:sum(r[k] for r in rows) for k in ('win','draw','loss','goalsFor','goalsAgainst')};partial.update(games=len(rows),plannedGames=40,complete=False,pairedSeedInferenceValid=False)
artifacts={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (Path(completion['numberedCheckpoint']),Path(completion['exportedModel']))}
record=dict(status='saved-stopped-user-weekly-threshold',weeklyRemainingPercent=35,threshold=35,run='MNG_MS3V3-20260929-r003',additionalTrainingSteps=completion['additionalSteps'],completion=completion,artifacts=artifacts,PS500PartialEvaluation=partial,FullExcluded=True,successEstablished=False,exhibitionPromoted=False,autoResumeAllowed=False)
(e/'saved-stop-summary.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
(e/'duel-500k/interrupted-report.json').write_text(json.dumps(dict(summary=partial,matches=rows,reason='weekly remaining 35%; incomplete or unstarted matches are not losses or draws'),indent=2),encoding='utf-8')
text=f'''# PS 재시도 결과와 사용량 중단 기록

2026-09-29 주간 잔여35%에 도달해 사용자 기준으로 저장·중단했다. **추가526,624환경step**, 누적PPO갱신6,165회, 양진영263,312step씩이다. 요청한 최소400k·최대1M 범위에 들어간다. 이전 실패Run의401,595step·시범 표본·반사실 비교의38,408환경결정은 이번 학습step에 합산하지 않았다. 성공은 입증되지 않았고 전시 모델에 추가하지 않았다.

## 계보와 단계

부모는 원본2M `MNG_MS3V3-20260925-r002/MNG_ManagerV2-2001834.pt`이며 SHA256은889169f22546f2684f7a7d4525c6d8fe21dde275c267c524b0b8aa2b7dacb380로 보존했다. 과거400k 격리 모델을 초기값·상대로 사용하지 않았다. 새Run은MNG_MS3V3-20260929-r003이다.

|단계|이번Run누적환경step|결과|
|---|---:|---|
|PS-R1|100,000|전체actor 모방/PPO로 기존 경기 능력 퇴행. 실패 저장|
|PS-R2|200,000|원본 backbone·비패스5명령을 복구·고정하고 패스/수신자만 학습|
|PS-R3|400,000|일반 경기 학습 완료. 원본60경기25승13무22패,163득점153실점. 유용한 패스 품질 실패|
|PS-R4|500,000|실제 비교 시범을 사용한 추가100k 완료. 별도PT/ONNX 보존|
|PS-R5|526,624|일반 경기 추가26,624 후 주간35% 중단.600k 미완료|

PS-R3의 정책 명령 연결 패스는9타격/2수신,4초 소유1,압박탈출0,수신 후 슛1,수신 후 득점0이었다. 승수25:22를 패스 학습 성공으로 확대하지 않았다. 이 단계는 아래 새 접촉 기술 이전 런타임이므로 최신 모델의 성적으로 재사용하지 않는다.

PS-R4 원본 직접평가는 **{len(rows)}경기만 완료: {partial['win']}승 {partial['draw']}무 {partial['loss']}패, {partial['goalsFor']}득점 {partial['goalsAgainst']}실점**. 계획40경기를 끝내지 못했고 진영/시드 쌍도 완결되지 않았다. 우위 판정·접전 추가 경기·품질 승격을 하지 않는다. 중단된 경기는 패배/무승부로 채우지 않는다. PS-R5 최신 저장본은 독립 평가하지 않았다.

## 구현 및 시범

골대 근처 패스 금지를 제거했다. 직접 수신자 선택은 골키퍼·후방·횡방향과 짧거나 먼 동료도 허용한다. 비효율적 전술 선택을 마스킹하지 않고, 시범/학습 손실로 선택을 가르친다. 패스 선택·수신자 출력 강제는 평가와 추론에 없다.

실제 발 접촉 후 찬 선수의 발/몸 재충돌을 잠시 분리하고 복원하는 패스 실행 수정과,공의 감쇠를 고려한 속도 계산을 별도 빌드에서 적용했다. 다른 선수의 차단은 유지한다. 원본과 후보 양쪽에 같은 기술을 적용한다. 따라서 결과를 모방학습만의 효과 또는 예전과 동일한 물리라고 주장하지 않는다. 약한 패스 힘·단순 회전·발만 분리한 중간 실패 버전의 소스/테스트/비교 기록도 보존했다.

180상황에서 원래 행동과 수신자별 대안을 동일 관측/마스크/난수열로 비교했다. 실제타격·의도수신·12초효용+0.15 기준의 양성25개(골키퍼21/필드동료4),훈련19/검증6을 얻었다. [검증 시범 목록](ms3-v3-PS-verified-teacher-20260929.md). 이는 단기 반사실 결과이며 자율 모델의 장기 승리 증거가 아니다. 검증6개는 보조손실에 넣지 않았다. 목표확률25%는학습시soft target이며추론강제값이아니다.

## 검증과 한계

- Manager EditMode219/219통과. 양팀×3방향×2거리×2이동속도의24실제 골키퍼 수신/충돌복원fixture통과.
- PS500의32실제 관측에서PyTorch와ONNX참조실행기확률최대차1.1920929e-7. 원본 backbone/비패스행 텐서 동일. onnxruntime 미설치 시도는 실패로그를 보존했고 설치 없이 ONNX ReferenceEvaluator로 확인했다. Unity Sentis 전체 동작 검증을 의미하지 않는다.
- 새 실행 빌드의 원본4상대×40경기=160경기 완료. Recover26/8/6,carry-shot은원본report참조,Balanced33/3/4,MS2 17/12/11. 후보의동일4상대평가는미완료다. Full은모든새수집/학습/평가에서제외했다.
- 실제 데스크톱 경기 화면04:44·0:0을 확인했으나 기존Unity.exe 오류창이중앙을가렸다. 300초경기는자동실행완료4:3이지만 전체연계시각검증/사람직접조작완료로세지않는다. `visual-attempt-400k.json`참조.
- 원본2M champion과현재전시빌드를유지했다. 성공거짓기록/전시패스모델추가는없다.

## 보존 및 재개 경계

최신PT/ONNX는 `results/MNG_MS3V3-20260929-r003/PS-526624.*`, 완료단계는 `PS-500000.*`, 이전100k/200k/400k도보존한다. mutable checkpoint.pt에는critic/Adam/RNG도있다. 상세해시는 `Logs/MNG-Rebuild/PS-Retry-20260929/saved-stop-summary.json`에있다.

STOP표시를유지하고자동재개하지않는다. 이후명시적재개요청과사용량조건확인뒤에만진행한다. 기존 `train-to-600000` 폴더를덮어쓰지말고새segment경로를지원하도록재개경로를정리해야한다. Unity물리상태는직렬화하지않아재개시새경기에서시작한다.500k미완료평가의원본도보존하고불완전진영쌍을완료평가로표기하지않는다.

[계획과 실패 이력](ms3-v3-PS-retry-plan-20260929.md),[초기 판단 예시](ms3-v3-PS-retry-teacher-examples-20260929.md).
'''
(root/'docs/soccer/training/ms3-v3-PS-retry-result-20260929.md').write_text(text,encoding='utf-8')
p=root/'docs/soccer/current-status.md';s=p.read_text(encoding='utf-8-sig');s=re.sub(r'^\|PS 재시도\|.*$', '|PS 재시도|MNG_MS3V3-20260929-r003 추가526,624step·PPO6,165회 저장 후 주간잔여35% 기준으로 중단.500k단계완료,600k미완료.400k패스품질실패,500k원본평가17경기만완료하여우위미판정.골대근처패스허용.성공/전시승격없음·자동재개금지. [결과](training/ms3-v3-PS-retry-result-20260929.md)|',s,flags=re.M);s=s.replace('새 학습량400k~1M은 이전401,595와 별도로 센다.','새 추가학습526,624step를 저장한 뒤 주간잔여35%에서 중단했다. 이전401,595와 별도로 센다.');p.write_text(s,encoding='utf-8')
p=root/'AGENTS.md';s=p.read_text(encoding='utf-8-sig');s=s.replace('## 현재 작업 경계','## 현재 작업 경계\n\n- PS 재시도 종료 상태(2026-09-29): r003 추가526,624step 저장 후 주간잔여35%에서 중단. 자동재개금지.500k평가17경기만완료,성공/전시승격없음. `docs/soccer/training/ms3-v3-PS-retry-result-20260929.md` 참조.',1);p.write_text(s,encoding='utf-8')
print(json.dumps(record,ensure_ascii=False,indent=2))
