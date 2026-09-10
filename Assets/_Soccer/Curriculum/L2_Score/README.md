# L2-Score

Mandatory scoring lesson between approved L2 and L3. Four Red agents play in an empty Stadium while Navy agents are disabled after environment registration. Each episode ends at the first Red goal, first own goal, or 30-second timeout; no score total is compared.

The active training config initializes without optimizer state from the approved L2-Find r004 checkpoint at 500,656 steps. That source passed two exact frozen 300-episode L2-Find evaluations at 96% and 95%; the original approved L2 r018 and all L2-Find runs remain preserved.

The ball is uniformly randomized inside the field with at least 1 m wall clearance and 5 m clearance from the Red goal center. A Red goal is success. An own goal is immediate failure with a group penalty. Timeout is failure. The approval gate is fixed: at least 95% success over an independent 300-episode evaluation and zero own goals.

A goal after one deliberate long pass may receive one group bonus. The pass must be explicitly kicked by the confirmed carrier toward a teammate 10-24 m away, start at least 30 m from the Navy goal, move the ball at least 8 m, improve the receiver's goal distance by at least 8 m, and finish with 0.35 seconds of stable control. Nearby handoffs, intervening contacts, and passes made when already within 30 m of goal cannot earn the bonus.

r001 used learning rate 0.0003 for 500,088 steps. Its best-looking 349,927 checkpoint reached only 30% in the same-seed frozen 100-episode evaluation versus the approved L2-Find baseline's 28%; possession fell from 88% to 80%, own goals rose from 2 to 5, and best goal distance worsened. r001 is preserved and is not resumed. r002 restarts from the approved L2-Find checkpoint with learning rate 0.00005 and beta 0.003. It reduces the one-time forward shot-attempt reward from 0.08 to 0.02 and raises the actual goal reward from 0.4 to 1.0. All success, own-goal, spawn, timeout, and long-pass conditions stay unchanged. This is one combined reward/optimizer experiment, so any change in quality must not be attributed to one setting alone.
