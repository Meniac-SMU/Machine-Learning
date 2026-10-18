"""Persistently run the authorized training, then build and verify the demo."""
import os
import subprocess
import sys
import time
import traceback
import zipfile
import hashlib
import shutil
from five_v11_continuation_common import *

DEMO=ROOT/'Builds/Manager5v5/demo-3600-20261017-r001'
POWERSHELL=shutil.which('pwsh')

def main():
    status=BASE/'pipeline-status.json'
    assert not status.exists(), 'Do not restart a failed pipeline automatically'
    env=os.environ.copy()
    env.update(OMP_NUM_THREADS='1',MKL_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',PYTHONUNBUFFERED='1')
    def execute(label,command,timeout=None):
        write(status,dict(state='running',operation=label,updated=time.time(),target=3600000,environments=24))
        with (BASE/(label+'-stdout.log')).open('w',encoding='utf-8') as out,(BASE/(label+'-stderr.log')).open('w',encoding='utf-8') as err:
            proc=subprocess.Popen(command,cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            write(BASE/'pipeline-active-process.json',dict(pid=proc.pid,operation=label,command=command))
            if label=='training':
                while proc.poll() is None:
                    progress_file=BASE/RUN/'orchestrator-status.json'
                    if progress_file.exists():
                        state=read(progress_file)
                        operation=state.get('operation','')
                        if state.get('state')=='running' and operation.startswith('train-'):
                            target=int(operation.split('-')[1])
                            folder=BASE/RUN/f'to-{target}-24env-cuda'
                            progress=folder/'progress.json'
                            updated=read(progress)['utc'] if progress.exists() else state['updated']
                            if folder.exists() and time.time()-updated>600:
                                (folder/'STOP').write_text('No progress for 600 seconds; save and stop.',encoding='utf-8')
                                write(BASE/'monitor-intervention.json',dict(reason='stale progress',operation=operation,utc=time.time()))
                    time.sleep(30)
                code=proc.returncode
            else:
                code=proc.wait(timeout=timeout)
        write(BASE/(label+'-exit.json'),dict(exitCode=code,ended=time.time()))
        assert code==0, f'{label} failed: {code}'
    try:
        assert POWERSHELL, 'PowerShell 7 is required; Windows PowerShell 5 is unsupported.'
        assert read(BASE/'resume-verification.json')['passed']
        execute('training',[sys.executable,str(ROOT/'Tools/Run-FiveV11Continuation.py'),'--mode','train'])
        execute('package-models',[sys.executable,str(ROOT/'Tools/Prepare-FiveDemo3600Models.py')])
        execute('build-demo',[POWERSHELL,'-NoProfile','-ExecutionPolicy','Bypass','-File',str(ROOT/'Tools/Build-FiveDemo.ps1'),'-RunName',DEMO.name])
        for label,extra in [('demo-smoke',[]),('demo-live',['--five-demo-live'])]:
            evidence=BASE/label
            evidence.mkdir()
            execute(label,[str(DEMO/'MENIAC.exe'),'-screen-width','1280','-screen-height','720','-screen-fullscreen','0',
                '--five-demo-smoke','--five-demo-evidence',str(evidence),'-logFile',str(evidence/'player.log'),*extra],timeout=900)
            assert read(evidence/'result.json')['passed']
        execute('record-demo-use',[POWERSHELL,'-NoProfile','-ExecutionPolicy','Bypass','-File',str(ROOT/'Tools/Build-Lifecycle.ps1'),
            '-Action','Use','-BuildDirectory',str(DEMO),'-Purpose','320만360만 포함 최종 시연 자동검사와1배속300초 검증','-Evidence',str(BASE/'demo-live/result.json')])
        execute('finalize',[sys.executable,str(ROOT/'Tools/Finalize-FiveV11Demo3600.py')])
        write(status,dict(state='complete',step=3600000,build=str(DEMO),completion=str(BASE/'completion.json'),ended=time.time()))
    except BaseException:
        write(status,dict(state='failed',error=traceback.format_exc(),ended=time.time()))
        raise

if __name__=='__main__':main()
