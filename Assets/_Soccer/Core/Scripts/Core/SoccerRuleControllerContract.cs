using Unity.MLAgents.Actuators;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// 규칙형 판단 결과와 ML-Agents 행동 브랜치의 공통 계약.
    /// 정책 계약 v2와 공유하므로 필드 순서와 0~2 값 의미를 Rule 전용으로 바꾸지 않는다.
    /// </summary>
    public readonly struct SoccerRuleCommand
    {
        public SoccerRuleCommand(int forward, int lateral, int rotation, int kick)
        {
            Forward = forward;
            Lateral = lateral;
            Rotation = rotation;
            Kick = kick;
        }

        public int Forward { get; }
        public int Lateral { get; }
        public int Rotation { get; }
        public int Kick { get; }

        public void WriteTo(ActionSegment<int> actions)
        {
            if (actions.Length < 4)
            {
                return;
            }

            actions[0] = Forward;
            actions[1] = Lateral;
            actions[2] = Rotation;
            actions[3] = Kick;
        }
    }

    /// <summary>
    /// 팀 전용 규칙 두뇌와 공통 선수 코드의 분리 경계.
    /// 판단은 구현체에 두고 이동·킥·관측은 AgentSoccer의 공통 경로를 사용한다.
    /// </summary>
    public interface ISoccerRuleController
    {
        void Configure(AgentSoccer agent, SoccerEnvController environment);
        SoccerRuleCommand Decide();
        void ResetController();
    }
}
