"""Rendered 1x match; use Computer Use to actually inspect the returned game window."""
import argparse
from pathlib import Path
import subprocess
import mng_psr_play as play
from mlagents_envs import env_utils

def main():
    p=argparse.ArgumentParser();p.add_argument('--candidate',required=True);p.add_argument('--output',required=True)
    p.add_argument('--seed',type=int,default=930001);a=p.parse_args();out=Path(a.output).resolve();out.mkdir()
    # A separately requested viewing match has its own stop scope. Preserve the
    # training STOP marker and never resume the training/evaluation queue.
    play.m.EVIDENCE=out
    (out/'view-request.json').write_text(play.json.dumps(dict(candidate=a.candidate,
        opponent=str(play.m.PARENT),candidateTeam='Red',opponentTeam='Navy',
        timeScale=1,matchSeconds=300,training=False,seed=a.seed),indent=2))
    original=play.UnityEnvironment;engine=play.EngineConfigurationChannel
    class SlowEngine(engine):
        def set_configuration_parameters(self,**kwargs):
            kwargs.update(time_scale=1,target_frame_rate=60,width=1280,height=720)
            return super().set_configuration_parameters(**kwargs)
    play.EngineConfigurationChannel=SlowEngine
    def visible(file_name,args):
        startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=1
        return subprocess.Popen([env_utils.validate_environment_path(file_name),*args],startupinfo=startup,
                                start_new_session=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    env_utils.launch_executable=visible
    def environment(*args,**kwargs):
        kwargs['no_graphics']=False;kwargs['base_port']=18500
        kwargs['additional_args']+=['-screen-fullscreen','0','-screen-width','1280','-screen-height','720']
        env=original(*args,**kwargs)
        # Evaluation Awake initially sets20; override again after reset in match.
        reset=env.reset
        def slow_reset():
            value=reset()
            for channel in kwargs['side_channels']:
                if isinstance(channel,engine):channel.set_configuration_parameters(time_scale=1,target_frame_rate=60)
            return value
        env.reset=slow_reset;return env
    play.UnityEnvironment=environment
    row=play.match((0,0,a.seed,0,a.candidate,str(play.m.PARENT),out,False))
    (out/'report.json').write_text(play.json.dumps(dict(matches=[row],summary=dict(goalsFor=row['goalsFor'],goalsAgainst=row['goalsAgainst'])),indent=2))
    print('Rendered match completed; visual observation must be recorded separately.',flush=True)

if __name__=='__main__':main()
