"""Summarize measured teacher alternatives without promoting them to policy wins."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('examples');p.add_argument('output');a=p.parse_args()
rows=[json.loads(x) for x in Path(a.examples).read_text(encoding='utf-8-sig').splitlines() if x]
assert len({r['seed'] for r in rows})==len(rows)
positive=[r for r in rows if r.get('preferredRecipient',0)>0]
lines=['# PS 실제 비교로 확인한 모범 답안','',
'동일한 관측·마스크·이후 원본 정책 난수열에서 한 번의 수신자 선택만 바꿔 비교했다. 아래 행은 실제 타격과 의도한 수신을 확인하고 12초 효용이 원래 행동보다 0.15 이상 높았던 사례다. 자율 학습 모델의 승리나 인과적 장기 우위를 보증하지 않는다. Full은 제외한다.','',
f'전체 {len(rows)}개 상황 중 유효 답안 {len(positive)}개. 훈련 {sum(r["split"]=="train" for r in positive)}개 / 분리 검증 {sum(r["split"]=="validation" for r in positive)}개. 나머지는 부모 선택 보존을 위한 음성 사례이며 패스 실행 금지가 아니다.','',
'|시드|분할|수신 슬롯|원래 효용|패스 효용|증가|패스 후 12초 득실점 차|후속 슛|','|---|---|---:|---:|---:|---:|---:|---:|']
for r in positive:
 v=next(v for v in r['variants'] if v['recipient']==r['preferredRecipient'])
 assert v['prefixExact'] and v['struck'] and v['received']
 lines.append(f'|{r["seed"]}|{r["split"]}|{r["preferredRecipient"]-1}|{r["baselineUtility"]:.3f}|{v["utility"]:.3f}|{v["utility"]-r["baselineUtility"]:.3f}|{v["goalMargin"]:.1f}|{v["shots"]}|')
lines += ['', '수신 슬롯0은 골키퍼다. 학습에는 train 행만 사용하고 validation 행은 보조손실에서 제외한다. 패스 soft target25%는 학습 목표이며 추론 확률을 강제하거나 제한하지 않는다. 공통 backbone과 비패스 명령 row는 원본2M 텐서를 고정한다.', '', '효용: 2×득실점 차 +0.3×종료 시 소유권 +0.003×공격 방향 깊이 +0.01×근접 상대 거리(최대8m) +0.08×슛. 종료 시 소유/위치와 짧은 시간 구간에 의존하므로 최종 전체 경기 평가가 필요하다.', '', f'원본 데이터: `{a.examples}`']
out=Path(a.output);assert not out.exists();out.write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps(dict(cases=len(rows),positives=len(positive),train=sum(r['split']=='train' for r in positive),validation=sum(r['split']=='validation' for r in positive))))
