namespace MachineLearning.Escape
{
    public enum EscapeEpisodeState
    {
        Resetting,
        Running,
        PlayerWin,
        EnemyWin,
        ResultFeedback
    }

    public enum EscapeRunMode
    {
        Human,
        PlayerTraining,
        EnemyTraining,
        JointTraining
    }

    public enum EscapePlayerControlMode
    {
        Human,
        Training,
        Inference,
        AutonomousHeuristic
    }
}
