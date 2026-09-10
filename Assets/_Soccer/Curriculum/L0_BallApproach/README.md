# L0 - Ball Approach

Red 네 명의 기존 관측·행동·역할 계약을 유지한 채 Navy를 비활성화한다. 매 에피소드마다 골키퍼를 제외한 Red 필드 선수 한 명을 순환 선택하고 공을 전방 1~2m, 측면 ±1m 범위에 배치한다. 우연한 킥을 소유로 오인하지 않도록 킥 행동을 마스킹한다.

- 제한 시간: 20초
- 성공: Red가 공에 접근해 0.75초 안정 소유를 확정
- 성공 보상: 팀 0.4 + 실제 소유자 개인 0.1
- 보조 보상: 접근 거리 개선, 최초 안정 소유
- 장기 목표: 500,000 aggregate steps / 16개 실행 인스턴스
- 승급 기준: 최근 평가 성공률 85% 이상, 평균 완료 10초 이하

생성 자산은 `Prefabs/StadiumEnvironment_CurriculumL0.prefab`과 `Scenes/Stadium4v4_CurriculumL0.unity`이다.
