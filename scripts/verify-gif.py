"""Pillow와 FFmpeg로 GIF 프레임·반복·구간·색상을 독립 검증한다. 제품 의존성 아님."""
import argparse,io,json,subprocess
from pathlib import Path
from PIL import Image,ImageChops,ImageStat
p=argparse.ArgumentParser();p.add_argument('test_directory');p.add_argument('--stress-directory');a=p.parse_args();root=Path(a.test_directory)
gif=Path((root/'gif-result-path.txt').read_text());image=Image.open(gif)
assert image.size==(160,120) and image.n_frames==30 and image.info.get('loop')==0
duration=0
for i in range(image.n_frames):
    image.seek(i);duration+=image.info['duration'];assert image.info['duration']>=20
assert duration==2000
image.seek(0);first=image.convert('RGB');image.seek(29);last=image.convert('RGB');assert first.tobytes()!=last.tobytes()
source=subprocess.check_output(['ffmpeg','-v','error','-ss','1','-i',str(root/'encoder-pattern.mp4'),'-vf','scale=160:120','-frames:v','1','-f','image2pipe','-vcodec','png','-'])
reference=Image.open(io.BytesIO(source)).convert('RGB')
error=sum(ImageStat.Stat(ImageChops.difference(first,reference)).mean)/3
assert error<8,error
assert first.getpixel((140,10))[0]>220 and first.getpixel((140,110))[2]>220
odd=Image.open(next((root/'gif-odd').glob('*.gif')));odd_duration=0
for i in range(odd.n_frames):odd.seek(i);odd_duration+=odd.info['duration']
assert odd.size==(161,121) and odd_duration==150
output={'gif':str(gif),'frames':image.n_frames,'duration_ms':duration,'size':image.size,'loop':0,'start_frame_reference_mean_error':round(error,3),'colors_and_orientation':True,'animation_changes':True,'odd_size':odd.size,'odd_duration_ms':odd_duration}
Path('artifacts/ui').mkdir(parents=True,exist_ok=True);Path('artifacts/ui/gif-decoder-results.json').write_text(json.dumps(output,indent=2),encoding='utf-8');print(json.dumps(output))
if a.stress_directory:
    with Image.open(next(Path(a.stress_directory).glob('*.gif'))) as stress:
        count=stress.n_frames;size=stress.size;loop=stress.info.get('loop');duration=0
        for i in range(count):stress.seek(i);duration+=stress.info['duration']
        assert count==900 and duration==30000 and loop==0
        result={'frames':count,'size':size,'duration_ms':duration,'loop':loop}
        Path('artifacts/ui/gif-stress-decoder-results.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
