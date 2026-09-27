// Run with NODE_PATH pointing to the installed Sharp dependency directory.
const sharp = require('sharp');
const path = require('path');
const root = path.resolve(__dirname, '..');
(async () => {
  for (const lang of ['ko', 'en']) for (const page of [2, 3, 4, 5]) {
    const name = `guide-${lang}-${page}`;
    await sharp(path.join(root, 'docs/soccer/exhibition/guide-art', name + '.svg'))
      .resize(1920, 960).png().toFile(path.join(root, 'Assets/_Soccer/Manager/Exhibition/Art', name + '.png'));
    console.log(name);
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
