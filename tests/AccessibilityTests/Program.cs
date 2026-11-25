using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Dedicado;
static class AccessibilityTests{
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Type FormType=Assembly.Load("Gerenciador-de-Dedicado").GetType("Gerenciador.MainForm",true)!;
    static T Field<T>(Form form,string name)=>(T)FormType.GetField(name,Instance)!.GetValue(form)!;
    static object? Call(Form form,string name,params object?[] args){
        if(System.Threading.SynchronizationContext.Current is not WindowsFormsSynchronizationContext)System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        return FormType.GetMethod(name,Instance)!.Invoke(form,args);
    }
    static void Assert(bool pass,string message){if(!pass)throw new Exception(message);}
    static void WaitFor(Func<bool> done,string message){var clock=System.Diagnostics.Stopwatch.StartNew();while(!done()&&clock.ElapsedMilliseconds<5000){Application.DoEvents();System.Threading.Thread.Sleep(5);}Assert(done(),message);}
    static void Capture(Form form,string name){
        var folder=Environment.GetEnvironmentVariable("GDD_QA_CAPTURE_DIR");if(string.IsNullOrWhiteSpace(folder))return;
        Directory.CreateDirectory(folder);var previous=form.Size;
        try{form.Size=new Size(1100,830);form.PerformLayout();Application.DoEvents();
            using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            image.Save(Path.Combine(folder,name+".png"),System.Drawing.Imaging.ImageFormat.Png);
        }finally{form.Size=previous;form.PerformLayout();Application.DoEvents();}
    }
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
    static void VerifyRounded(Button button){
        if(SystemInformation.HighContrast)return;
        Assert(button.FlatAppearance.BorderSize==0,"Native square border: "+button.Text);
        Assert(button.GetType().Name=="ModernButton","Native paint adapter still present: "+button.Text);
        Assert(button.Region==null,"Native clip may leave unpainted pixels: "+button.Text);
        // WM_PRINT draws via GDI and may not set the alpha channel. Compare opaque RGB.
        using var image=new Bitmap(button.Width,button.Height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using(var canvas=Graphics.FromImage(image))canvas.Clear(Color.Magenta);
        button.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
        var expected=button.Parent!.BackColor.ToArgb()&0xffffff;
        int Rgb(int x,int y)=>image.GetPixel(x,y).ToArgb()&0xffffff;
        void Match(int x,int y,string edge){
            int actual=Rgb(x,y);
            if(actual==expected)return;
            var folder=Environment.GetEnvironmentVariable("GDD_QA_CAPTURE_DIR");
            if(!string.IsNullOrWhiteSpace(folder)){Directory.CreateDirectory(folder);image.Save(Path.Combine(folder,"failed-button.png"),System.Drawing.Imaging.ImageFormat.Png);}
            throw new Exception($"{edge} halo: {button.Text} at {x},{y}; expected #{expected:X6}, actual #{actual:X6}");
        }
        for(int x=0;x<image.Width;x++){Match(x,0,"Top");Match(x,image.Height-1,"Bottom");}
        for(int y=0;y<image.Height;y++){Match(0,y,"Left");Match(image.Width-1,y,"Right");}
        int inset=Math.Max(1,button.LogicalToDeviceUnits(3));
        Assert(Rgb(image.Width/2,inset)!=expected,"Missing rounded border: "+button.Text);
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
        var calculate=FormType.GetMethod("CalculateViewport",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach(int dpi in new[]{96,120,144,168,192}){
            int Scale(int n)=>(n*dpi+48)/96;
            var minimum=new Size(Scale(840),Scale(650));
            var small=(Size)calculate.Invoke(null,new object[]{new Size(Scale(640),Scale(420)),minimum.Width,minimum.Height,Scale(17),Scale(17)})!;
            var large=(Size)calculate.Invoke(null,new object[]{new Size(Scale(1200),Scale(900)),minimum.Width,minimum.Height,Scale(17),Scale(17)})!;
            Assert(small==minimum&&large==new Size(Scale(1200),Scale(900)),"Viewport calculation at DPI "+dpi);
            Console.WriteLine("VIEWPORT_GEOMETRY_SIMULATED_DPI_PASS "+dpi);
        }
        string fixture=Path.Combine(Path.GetTempPath(),"gdd-a11y-"+Guid.NewGuid().ToString("N"));
        try{
            Directory.CreateDirectory(fixture);
            var savePreference=FormType.GetMethod("TrySaveUpdatePreference",BindingFlags.Static|BindingFlags.NonPublic)!;
            var preference=Path.Combine(fixture,"preferences","check-updates.txt");
            bool Save(string path,bool value)=>(bool)savePreference.Invoke(null,new object[]{path,value})!;
            Assert(Save(preference,true)&&File.ReadAllText(preference)=="true","Preference enable failed");
            Assert(Save(preference,false)&&!File.Exists(preference),"Preference disable failed");
            Assert(Save(preference,false),"Preference disable not idempotent");
            var blocked=Path.Combine(fixture,"blocked");File.WriteAllText(blocked,"preserve");
            Assert(!Save(Path.Combine(blocked,"check-updates.txt"),true),"Failed preference incorrectly reported success");
            Assert(!Save(Path.GetDirectoryName(preference)!,false),"Directory deletion incorrectly reported success");
            Assert(File.ReadAllText(blocked)=="preserve","Failed preference changed unrelated file");
            Console.WriteLine("PREFERENCE_STORAGE_PASS");
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
                    foreach(var b in buttons){Reachable(host,b);VerifyRounded(b);}
                    foreach(var b in Field<List<Button>>(form,"navButtons"))VerifyRounded(b);
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
                Console.WriteLine("KEYBOARD_TAB_PASS "+theme);Console.WriteLine("ROUNDED_PIXEL_PERIMETER_PASS "+theme);Capture(form,"home-"+theme);
                if(!SystemInformation.HighContrast){
                    var prior=form.BackColor;
                    Call(form,"ShowPreferences");Application.DoEvents();
                    Assert(Field<Panel>(form,"internalPage").Visible&&!host.Visible&&!mainHost.Visible,"Preferences not embedded");Capture(form,"preferences-"+theme);
                    Call(form,"ApplyLiveTheme",theme=="dark"?"light":"dark");Application.DoEvents();
                    Assert(form.BackColor.ToArgb()!=prior.ToArgb(),"Live palette not applied");
                    Call(form,"ApplyLiveTheme",theme);Call(form,"ShowHome");Application.DoEvents();
                    Assert(form.BackColor.ToArgb()==prior.ToArgb()&&host.Visible,"Theme or navigation restore failed");
                    Call(form,"ShowSessionLog");Application.DoEvents();Assert(Field<Panel>(form,"internalPage").Visible,"History not embedded");Capture(form,"activity-"+theme);
                    Call(form,"ShowHome");Call(form,"ShowGettingStarted");Application.DoEvents();Assert(Field<Panel>(form,"internalPage").Visible,"Guide not embedded");Capture(form,"guide-"+theme);
                    Call(form,"ShowHome");Application.DoEvents();Console.WriteLine("EMBEDDED_PAGES_AND_LIVE_THEME_PASS "+theme);
                    Call(form,"OpenTask",2,false,null,false,"mods");Call(form,"Busy",true);
                    using(var content=new Panel()){
                        var cancel=new Button{Text="Cancelar"};content.Controls.Add(cancel);cancel.Click+=(_,_)=>Call(form,"FinishTaskPage",false);
                        var pending=(Task<bool>)Call(form,"ShowTaskPage","mods",content,480,420,cancel)!;Application.DoEvents();
                        Assert(Field<Panel>(form,"taskPageHost").Visible&&!mainHost.Visible,"Task page not embedded");
                        cancel.PerformClick();
                        WaitFor(()=>pending.IsCompleted,"Task page did not finish");
                        Assert(pending.IsCompletedSuccessfully&&!pending.Result&&mainHost.Visible,"Task page cancellation did not restore workspace");
                    }
                    Call(form,"Busy",false);Call(form,"ShowHome");Application.DoEvents();Console.WriteLine("EMBEDDED_TASK_CANCEL_PASS "+theme);
                    Call(form,"OpenTask",2,false,null,false,"mods");
                    var cluster=Engine.LoadClusterDirectory(world);
                    var plan=new Plan(cluster,cluster,install,"",cluster.Shards.Select(sh=>new ShardPlan(sh,System.Text.Encoding.UTF8.GetBytes("return {}"),[],[])).ToArray(),"fixture",true,true);
                    Call(form,"Busy",true);
                    var editing=(Task<Dictionary<string,byte[]>?>)Call(form,"EditVisual",plan,false)!;
                    WaitFor(()=>Field<Panel>(form,"taskPageHost")!=null,"Catalog did not open");
                    var catalog=Field<Panel>(form,"taskPageHost");
                    var shardSelector=Desc(catalog).OfType<ComboBox>().Single(c=>c.AccessibleName=="Shard selecionado");
                    shardSelector.SelectedItem="Caves";
                    Assert(Desc(catalog).OfType<Label>().Any(l=>l.Text=="ID / link:")&&Desc(catalog).OfType<Label>().Any(l=>l.Text=="Filtro:"),"Catalog input labels missing");
                    var idField=Desc(catalog).OfType<TextBox>().Single(c=>c.AccessibleName=="Adicionar por ID ou link Workshop");
                    var add=Desc(catalog).OfType<Button>().Single(c=>c.Text=="Adicionar / ativar");
                    WaitFor(()=>add.Enabled,"Offline catalog lookup did not finish");idField.Text="123456789";add.PerformClick();
                    var review=Desc(catalog).OfType<Button>().Single(c=>c.Text=="Revisar plano");
                    WaitFor(()=>review.Enabled,"Offline catalog add did not finish");foreach(var b in Desc(catalog).OfType<Button>())VerifyRounded(b);Capture(form,"catalog-"+theme);review.PerformClick();
                    WaitFor(()=>editing.IsCompleted,"Catalog review did not finish");
                    Assert(editing.IsCompletedSuccessfully&&editing.Result!=null,"Catalog result missing");
                    Assert(Lua.Active(System.Text.Encoding.UTF8.GetString(editing.Result!["Caves"])).ContainsKey("123456789")&&Lua.Active(System.Text.Encoding.UTF8.GetString(editing.Result["Master"])).Count==0,"Catalog changed wrong shard");
                    Call(form,"Busy",false);
                    var diag=(Task)Call(form,"Diagnose")!;
                    WaitFor(()=>Field<Panel>(form,"taskPageHost") is Panel page&&page.Name=="task-diagnostic"&&Desc(page).OfType<DataGridView>().Any()||diag.IsCompleted,"Diagnostic did not open");
                    Assert(!diag.IsCompleted,"Diagnostic ended before display: "+Field<TextBox>(form,"log").Text);
                    var diagnosticPage=Field<Panel>(form,"taskPageHost");
                    Assert(Desc(diagnosticPage).OfType<DataGridView>().Any(),"Diagnostic results missing: "+Field<TextBox>(form,"log").Text);
                    Assert(Desc(diagnosticPage).OfType<Label>().Any(l=>l.Text=="Pesquisar:"),"Diagnostic search label missing");foreach(var b in Desc(diagnosticPage).OfType<Button>())VerifyRounded(b);Capture(form,"diagnostic-"+theme);
                    Desc(diagnosticPage).OfType<Button>().Single(b=>b.AccessibleName=="Voltar do diagnóstico").PerformClick();
                    WaitFor(()=>diag.IsCompleted,"Diagnostic did not return");
                    Assert(diag.IsCompletedSuccessfully&&mainHost.Visible,"Diagnostic did not restore workspace");
                    var backupRoot=Path.Combine(fixture,"backup-fixture");Directory.CreateDirectory(backupRoot);
                    FormType.GetField("backupRootOverride",Instance)!.SetValue(form,backupRoot);
                    var backupTask=(Task)Call(form,"OpenBackups")!;
                    WaitFor(()=>Field<Panel>(form,"taskPageHost") is Panel page&&page.Name=="task-backups"||backupTask.IsCompleted,"Backups did not open");
                    Assert(!backupTask.IsCompleted,"Backup page ended before display");
                    var backupPage=Field<Panel>(form,"taskPageHost");
                    Assert(Desc(backupPage).OfType<Label>().Any(l=>l.Text.Contains("Nenhum backup encontrado")),"Missing empty backup state");foreach(var b in Desc(backupPage).OfType<Button>())VerifyRounded(b);Capture(form,"backups-"+theme);
                    Desc(backupPage).OfType<Button>().Single(b=>b.AccessibleName=="Voltar dos backups").PerformClick();
                    WaitFor(()=>backupTask.IsCompleted,"Backups did not return");
                    Assert(backupTask.IsCompletedSuccessfully&&mainHost.Visible,"Backups did not restore workspace");
                    FormType.GetField("backupRootOverride",Instance)!.SetValue(form,null);
                    Call(form,"ShowHome");Application.DoEvents();Console.WriteLine("CATALOG_SHARD_DIAGNOSTIC_BACKUP_PASS "+theme);
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