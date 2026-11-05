"""화면 캡처 검증용 합성 창. 사용자의 실제 데이터를 사용하지 않는다."""
import ctypes,sys,time
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
import tkinter as tk
root=tk.Tk();root.title('Clip QA fixture');root.geometry('1000x700+30+30');root.attributes('-topmost',True)
canvas=tk.Canvas(root,bg='#e9eef5',highlightthickness=0);canvas.pack(fill='both',expand=True)
canvas.create_text(30,40,anchor='w',text='Clip — synthetic capture test',font=('Arial',24,'bold'),fill='#142337')
for i,color in enumerate(['#ed4b4b','#e8bb35','#48b983','#4897df','#9463d9']):
    canvas.create_rectangle(80,120+i*90,880,210+i*90,fill=color,outline='')
    canvas.create_text(110,160+i*90,anchor='w',text=f'BAND {i+1} / Capture, annotate, save',font=('Arial',20,'bold'),fill='white')
if '--animate' in sys.argv:
    tick=canvas.create_text(100,660,anchor='w',text='',font=('Arial',20,'bold'),fill='#142337')
    marker=canvas.create_rectangle(100,620,130,642,fill='#142337',outline='')
    started=time.monotonic()
    def animate():
        elapsed=time.monotonic()-started
        canvas.itemconfigure(tick,text=f'Synthetic timer {elapsed:07.2f}s')
        x=100+int(elapsed*120)%700;canvas.coords(marker,x,620,x+30,642)
        root.after(30,animate)
    animate()
root.bind('<Escape>',lambda _:root.destroy());root.mainloop()
