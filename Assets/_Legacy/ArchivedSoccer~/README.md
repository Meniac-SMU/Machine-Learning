# 보관된 Soccer 실험

이 `~` 폴더는 현재 Soccer 4v4에 포함되지 않는 역사 Asset을 Unity import 밖에 보존한다.

- `Pass`: 폐기된 Pass 중심 강화학습 전술 실험. 현행 비학습 비교군은 `Assets/_Soccer/Teams/Rule_PHC`다.
- `MLAgentsSamples`: 초기 참고에 사용한 ML-Agents Soccer sample.

`Pass`와 현행 Rule 일부 `.meta`는 같은 GUID를 가질 수 있다. `~`를 제거하거나 두 사본을 import 대상 위치에 동시에 꺼내면 중복 GUID가 생기므로 금지한다.

복구가 필요하면 [Asset 구조](../../../docs/project/asset-layout.md)에 따라 대상과 GUID를 먼저 조사하고 한 사본만 별도 승격한다.
