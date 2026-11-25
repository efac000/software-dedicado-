using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Dedicado;
static class AccessibilityTests{
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Type FormType=Assembly.Load("Gerenciador-de-Dedicado").GetType("Gerenciador.MainForm",true)!;
    static T Field<T>(Form form,string name)=>(T)FormType.GetField(name,Instance)!.GetValue(form)!;
    static object? Call(Form form,string name,params object?[] args)=>FormType.GetMethod(name,Instance)!.Invoke(form,args);
    static void Assert(bool pass,string message){if(!pass)throw new Exception(message);}
    static IEnumerable<Control> Desc(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Desc(x)));
    static Form Create(string theme){
        FormType.GetMethod("ConfigurePalette",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[theme]);
        var form=(Form)Activator.CreateInstance(FormType,[true])!;
        form.Show();Application.DoEvents();Call(form,"ShowHome");return form;
    }
    static double Luminance(Color c){
        double Linear(int v){double s=v/255d;return s<=0.04045?s/12.92:Math.Pow((s+0.055)/1.055,2.4);}
        return 0.2126*Linear(c.R)+0.7152*Linear(c.G)+0.0722*Linear(c.B);
    }
    static double Contrast(Color fg,Color bg){
        var a=Luminance(fg);var b=Luminance(bg);return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
    }
    static void Reachable(Panel host,Control c){
        Assert(c.Visible&&c.Width>0&&c.Height>0,"Missing control "+c.Name+" "+c.Text);
        host.ScrollControlIntoView(c);Application.DoEvents();
        var relative=host.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
        Assert(host.ClientRectangle.IntersectsWith(relative),"Cannot scroll to "+c.Text);
    }
    [STAThread] public static int MainEntry(string[] args){
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        if(args.Length==1&&args[0]=="--serve-uia"){
            using var form=Create("dark");
            Console.WriteLine("QA_HWND="+form.Handle.ToInt64());Console.Out.Flush();
            using var timeout=new System.Windows.Forms.Timer{Interval=45000};timeout.Tick+=(_,_)=>form.Close();timeout.Start();Application.Run(form);return 0;
        }
        string fixture=Path.Combine(Path.GetTempPath(),"gdd-a11y-"+Guid.NewGuid().ToString("N"));
        try{
            Directory.CreateDirectory(fixture);
            var world=Path.Combine(fixture,"Mundo QA");var install=Path.Combine(fixture,"dedicated");
            Directory.CreateDirectory(install);
            File.WriteAllText(Path.Combine(fixture,"marker.txt"),"original");
            foreach(var shard in new[]{"Master","Caves"}){
                Directory.CreateDirectory(Path.Combine(world,shard));
                File.WriteAllText(Path.Combine(world,shard,"server.ini"),"[SHARD]");
                File.WriteAllText(Path.Combine(world,shard,"modoverrides.lua"),"return {}");
            }
            File.WriteAllText(Path.Combine(world,"cluster.ini"),"[NETWORK]\ncluster_name = QA Accessibility");
            foreach(var theme in new[]{"dark","light"}){
                using var form=Create(theme);
                Field<TextBox>(form,"install").Text=install;
                Field<TextBox>(form,"destinationRoot").Text=world;Call(form,"RefreshClusters");
                var home=Field<TableLayoutPanel>(form,"home");var host=Field<Panel>(form,"homeHost");
                var mainHost=Field<Panel>(form,"mainHost");var main=Field<TableLayoutPanel>(form,"mainLayout");
                var buttons=Desc(home).OfType<Button>().ToArray();
                Assert(buttons.Length==12,"Expected 12 home actions");
                foreach(var b in buttons){
                    Assert(!string.IsNullOrWhiteSpace(b.AccessibilityObject.Name),"Unnamed button");
                    Assert(b.AccessibilityObject.Role==AccessibleRole.PushButton,"Wrong accessible role");
                    Assert(b.TabStop,"Button absent from tab order");
                    Assert(Contrast(b.ForeColor,b.BackColor)>=4.5,"Low contrast "+b.Text);
                }
                foreach(var label in Desc(home).OfType<Label>())Assert(Contrast(label.ForeColor,label.BackColor)>=4.5,"Low contrast label");
                var shortcuts=buttons.Select(b=>b.Text.IndexOf('&')).Count(i=>i>=0);
                Assert(shortcuts==12,"Missing keyboard shortcut");
                Assert(buttons.Select(b=>char.ToUpperInvariant(b.Text[b.Text.IndexOf('&')+1])).Distinct().Count()==12,"Duplicate shortcuts");
                foreach(var size in new[]{new Size(1536,864),new Size(1100,830),new Size(850,680),new Size(683,512),new Size(640,420)}){
                    form.Size=size;form.PerformLayout();Application.DoEvents();
                    if(form.DeviceDpi==96&&size.Width>=850&&size.Height>=680)Assert(!host.HorizontalScroll.Visible&&!host.VerticalScroll.Visible,"Unnecessary scrollbars in normal window");
                    foreach(var b in buttons)Reachable(host,b);
                    var feedback=buttons.Single(b=>b.Text.Contains("Feedbac"));
                    feedback.Focus();Application.DoEvents();
                    Assert(feedback.Focused,"Feedback not keyboard focusable");
                    Console.WriteLine($"VIEWPORT_SCROLL_CONTRAST_PASS {theme} {size.Width}x{size.Height} DPI={form.DeviceDpi}");
                }
                form.Size=new Size(640,420);Application.DoEvents();
                Call(form,"ShowHome");Application.DoEvents();
                var visited=new HashSet<Button>();
                for(int i=0;i<36;i++){form.SelectNextControl(form.ActiveControl,true,true,true,true);Application.DoEvents();if(form.ActiveControl is Button b)visited.Add(b);}
                Assert(visited.Count==22&&buttons.All(visited.Contains),"Tab traversal incomplete "+visited.Count);
                Console.WriteLine("KEYBOARD_TAB_PASS "+theme);
                if(!SystemInformation.HighContrast){
                    var prior=form.BackColor;
                    Call(form,"ShowPreferences");Application.DoEvents();
                    Assert(Field<Panel>(form,"internalPage").Visible&&!host.Visible&&!mainHost.Visible,"Preferences not embedded");
                    Call(form,"ApplyLiveTheme",theme=="dark"?"light":"dark");Application.DoEvents();
                    Assert(form.BackColor.ToArgb()!=prior.ToArgb(),"Live palette not applied");
                    Call(form,"ApplyLiveTheme",theme);Call(form,"ShowHome");Application.DoEvents();
                    Assert(form.BackColor.ToArgb()==prior.ToArgb()&&host.Visible,"Theme or navigation restore failed");
                    Call(form,"ShowSessionLog");Application.DoEvents();Assert(Field<Panel>(form,"internalPage").Visible,"History not embedded");
                    Call(form,"ShowHome");Call(form,"ShowGettingStarted");Application.DoEvents();Assert(Field<Panel>(form,"internalPage").Visible,"Guide not embedded");
                    Call(form,"ShowHome");Application.DoEvents();Console.WriteLine("EMBEDDED_PAGES_AND_LIVE_THEME_PASS "+theme);
                    Call(form,"OpenTask",2,false,null,false,"mods");Call(form,"Busy",true);
                    using(var content=new Panel()){
                        var cancel=new Button{Text="Cancelar"};content.Controls.Add(cancel);cancel.Click+=(_,_)=>Call(form,"FinishTaskPage",false);
                        var pending=(Task<bool>)Call(form,"ShowTaskPage","mods",content,480,420,cancel)!;Application.DoEvents();
                        Assert(Field<Panel>(form,"taskPageHost").Visible&&!mainHost.Visible,"Task page not embedded");
                        cancel.PerformClick();
                        for(int i=0;i<100&&!pending.IsCompleted;i++){Application.DoEvents();System.Threading.Thread.Sleep(5);}
                        Assert(pending.IsCompletedSuccessfully&&!pending.Result&&mainHost.Visible,"Task page cancellation did not restore workspace");
                    }
                    Call(form,"Busy",false);Call(form,"ShowHome");Application.DoEvents();Console.WriteLine("EMBEDDED_TASK_CANCEL_PASS "+theme);
                }
                var state=Field<Label>(form,"homeState");
                Assert(state.AccessibilityObject.Name?.Contains("QA Accessibility")==true,"Status accessible name not updated");
                Assert(state.LiveSetting==System.Windows.Forms.Automation.AutomationLiveSetting.Polite,"Status missing live setting");
                using(var timer=new System.Windows.Forms.Timer{Interval=50}){
                    Exception? error=null;bool verified=false;
                    timer.Tick+=(_,_)=>{
                        var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=form);
                        if(dialog==null)return;
                        timer.Stop();
                        try{
                            Assert(dialog.Text=="Feedback e suporte","Wrong modal");
                            var fields=Desc(dialog).ToArray();
                            var build=fields.OfType<TextBox>().Single();
                            Assert(build.ReadOnly&&build.AccessibilityObject.Name?.Contains("build")==true,"Build field not accessible");
                            Assert(fields.OfType<Button>().Count()==(SystemInformation.HighContrast?5:6),"Feedback actions missing");
                            Assert(fields.OfType<Label>().Any(l=>l.Text.Contains("públicos")&&l.Text.Contains("tokens")),"Privacy warning missing");
                            Assert(fields.OfType<Button>().All(b=>!string.IsNullOrWhiteSpace(b.AccessibilityObject.Name)),"Unnamed dialog action");
                            verified=true;
                        }catch(Exception ex){error=ex;}finally{dialog.DialogResult=DialogResult.Cancel;}
                    };
                    timer.Start();buttons.Single(b=>b.Text.Contains("Feedbac")).PerformClick();
                    if(error!=null)throw error;Assert(verified,"Feedback modal not verified");
                }
                Console.WriteLine("FEEDBACK_MODAL_PRIVACY_AND_CANCEL_PASS "+theme);
                if(!SystemInformation.HighContrast){
                    var notice=FormType.GetNestedType("Notice",BindingFlags.NonPublic)!;
                    var show=notice.GetMethod("Show",new[]{typeof(IWin32Window),typeof(string),typeof(string),typeof(MessageBoxButtons),typeof(MessageBoxIcon),typeof(MessageBoxDefaultButton)})!;
                    foreach(var buttonsMode in new[]{MessageBoxButtons.YesNo,MessageBoxButtons.YesNoCancel}){
                        using var timer=new System.Windows.Forms.Timer{Interval=50};
                        Exception? error=null;bool verified=false;
                        var defaultButton=buttonsMode==MessageBoxButtons.YesNo?MessageBoxDefaultButton.Button2:MessageBoxDefaultButton.Button3;
                        timer.Tick+=(_,_)=>{
                            var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f!=form);
                            if(dialog==null)return;
                            timer.Stop();
                            try{
                                Assert(dialog.FormBorderStyle==FormBorderStyle.None,"Native modal border");
                                Assert(((Button)dialog.AcceptButton!).DialogResult==(buttonsMode==MessageBoxButtons.YesNo?DialogResult.No:DialogResult.Cancel),"Unsafe default confirmation");
                                var close=Desc(dialog).OfType<Button>().Single(b=>b.AccessibleName=="Fechar janela");
                                verified=true;close.PerformClick();
                            }catch(Exception ex){error=ex;dialog.DialogResult=DialogResult.Cancel;}
                        };
                        timer.Start();var returned=(DialogResult)show.Invoke(null,new object[]{form,"Mensagem QA, nenhuma operação será executada.","Confirmação QA",buttonsMode,MessageBoxIcon.Warning,defaultButton})!;
                        if(error!=null)throw error;
                        Assert(verified&&returned==(buttonsMode==MessageBoxButtons.YesNo?DialogResult.No:DialogResult.Cancel),"Custom modal unsafe close");
                    }
                    Console.WriteLine("CUSTOM_MODAL_SAFE_DEFAULT_AND_CLOSE_PASS "+theme);
                }
                buttons.Single(b=>b.Text.StartsWith("&Gerenciar mods")).PerformClick();Application.DoEvents();
                Assert(mainHost.Visible&&!host.Visible,"Navigation failed");
                foreach(var name in new[]{"analyze","reset","diagnose","backups","website"})Reachable(mainHost,Field<Button>(form,name));
                foreach(var name in new[]{"root","destinationRoot","install"})Assert(!string.IsNullOrWhiteSpace(Field<TextBox>(form,name).AccessibilityObject.Name),"Path without name");
                var grid=Field<DataGridView>(form,"grid");
                Assert(Contrast(grid.DefaultCellStyle.ForeColor,grid.DefaultCellStyle.BackColor)>=4.5,"Grid contrast");
                Assert(Contrast(grid.DefaultCellStyle.SelectionForeColor,grid.DefaultCellStyle.SelectionBackColor)>=4.5,"Grid selection contrast");
                Field<CheckBox>(form,"exclusive").Checked=true;Call(form,"ShowHome");Application.DoEvents();
                Assert(!Field<CheckBox>(form,"exclusive").Checked&&!Field<Button>(form,"reset").Enabled,"Unsafe retained confirmation");
                Call(form,"Busy",true);Assert(buttons.All(b=>!b.Enabled),"Busy bypass");Call(form,"Busy",false);
                form.Close();
            }
            foreach(var shard in new[]{"Master","Caves"})Assert(File.ReadAllText(Path.Combine(world,shard,"modoverrides.lua"))=="return {}","QA navigation wrote mod file");
            Console.WriteLine("ACCESSIBILITY_QA_PASS");return 0;
        }finally{if(Directory.Exists(fixture))Directory.Delete(fixture,true);}
    }
    [STAThread] public static int Main(string[] args)=>MainEntry(args);
}