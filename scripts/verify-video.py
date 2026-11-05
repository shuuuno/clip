"""FFmpeg/ffprobe와 Pillow를 이용한 개발용 독립 MP4 검증. 배포하지 않는다."""
import argparse,json,subprocess
from pathlib import Path
from PIL import Image
import io
p=argparse.ArgumentParser();p.add_argument('test_directory');p.add_argument('--captures',default='artifacts/ui/recording-data/captures');p.add_argument('--report',default='artifacts/ui/recording-media-results.json');a=p.parse_args()
root=Path(a.test_directory)
def probe(path):
    return json.loads(subprocess.check_output(['ffprobe','-v','error','-show_entries','stream=codec_name,codec_type,width,height,duration,nb_frames:format=duration','-of','json',str(path)]))
def frame(path,at):
    return Image.open(io.BytesIO(subprocess.check_output(['ffmpeg','-v','error','-ss',str(at),'-i',str(path),'-frames:v','1','-f','image2pipe','-vcodec','png','-']))).convert('RGB')
pattern=root/'encoder-pattern.mp4';info=probe(pattern)
assert abs(float(info['format']['duration'])-4)<.01
first=frame(pattern,0);last=frame(pattern,3.8)
assert first.size==(320,240)
assert first.getpixel((100,30))[0]>220 and first.getpixel((100,210))[2]>220
assert first.tobytes()!=last.tobytes()
fourk=probe(root/'encoder-4k.mp4');s=fourk['streams'][0]
assert (s['width'],s['height'])==(3840,2160) and s['codec_name']=='h264'
four_k_frame=frame(root/'encoder-4k.mp4',0);assert four_k_frame.size==(3840,2160) and four_k_frame.getpixel((100,100))[1]>160
results={'pattern_duration_and_orientation':True,'pattern_changes_over_time':True,'four_k':fourk,'screen_recordings':[]}
for path in sorted(Path(a.captures).glob('Clip-*.mp4')):
    meta=probe(path);assert len(meta['streams'])==1 and meta['streams'][0]['codec_name']=='h264'
    assert float(meta['format']['duration'])>0
    subprocess.run(['ffmpeg','-v','error','-i',str(path),'-f','null','-'],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
    results['screen_recordings'].append({'path':str(path),'decoded':True,**meta})
Path(a.report).write_text(json.dumps(results,indent=2),encoding='utf-8')
print(json.dumps({'pattern':True,'four_k':True,'decoded_screen_recordings':len(results['screen_recordings'])}))
