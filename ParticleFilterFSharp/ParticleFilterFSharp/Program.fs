open ParticleFilter.ParticleFilter; 

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols

open FSharp.Charting
open FSharp.Charting.ChartTypes

open System
open System.Drawing
open System.Windows.Forms
open System.Reactive
open System.Reactive.Linq

open EventEx;

[<EntryPoint>]
[<STAThread>]
let main argv = 
    Application.EnableVisualStyles()
    Application.SetCompatibleTextRenderingDefault false

    // Create a form to host chart instead of showing chart directly,
    //  so synchronisation context is created for Rx
    let hostForm = new Form(Visible = true, TopMost = true, Width = 500, Height = 500)

    let random = new System.Random();
    let particles = create_particles 100.0 1000 random

    let robot = { x = 50.0<m>; y = 50.0<m>; orientation = 0.0 }
    let initialWorld =  { robot = robot; particleStates = particles }

    let control = { move = 1.0<m>; rotate = 0.05 }
    let markers = [|(20.0<m>, 20.0<m>); (80.0<m>, 80.0<m>); (20.0<m>, 80.0<m>); (80.0<m>, 20.0<m>)|]
    let noise = { sense = 5.0; forward = 0.05; turn = 0.05 }
    let updateParams = { control = control; markers = markers; noise = noise }

    let update' = update random updateParams

    let worldUpdate = Observable.Interval(TimeSpan.FromMilliseconds(500.0))
                                .ObserveOn(WindowsFormsSynchronizationContext.Current)
                                |> Observable.scan (fun currentWorld t -> currentWorld |> update') initialWorld

    // Observable.Interval is a cold observable, warm up at this point to ensure that both charts are
    //  displaying the same simulation.
    let sharedUpdate = worldUpdate.Publish().RefCount()

    let chartUpdate = (Observable.Return initialWorld).Concat sharedUpdate

    let particlesUpdate = chartUpdate |> Observable.map (fun w -> w.particleStates |> Seq.map(fun p -> (p.x, p.y)))
    let robotUpdate = chartUpdate |> Observable.map(fun w -> [|w.robot|] |> Seq.map(fun p -> (p.x, p.y) ))

    let particlesChart = LiveChart.Point(particlesUpdate)  
    let robotActualPositionChart = LiveChart.Point(robotUpdate).WithMarkers(System.Drawing.Color.Red)

    let chart = Chart.Combine([ particlesChart
                                robotActualPositionChart ])
                     .WithXAxis(Min = 0.0, Max = 100.0)
                     .WithYAxis(Min = 0.0, Max = 100.0)

    let chartControl = new ChartControl(chart)
    chartControl.Dock <- DockStyle.Fill
    hostForm.Controls.Add(chartControl)

// This can be uncommented to render out each frame to a file
// generate animated gif using imagemagick: magick -delay 20 -loop 0 *.png pf.gif
//    let mutable image_number = 0;
//    chartUpdate.Take(30)
//               .Subscribe(fun world ->
//                                let staticParticles = world.particleStates |> Seq.map(fun p -> (p.x, p.y))
//                                let staticRobot = [|world.robot|] |> Seq.map(fun p -> (p.x, p.y) )
//                                let staticChart = Chart.Combine([ Chart.Point(staticParticles)
//                                                                  Chart.Point(staticRobot).WithMarkers(System.Drawing.Color.Red) ])
//                                                       .WithXAxis(Min = 0.0, Max = 100.0)
//                                                       .WithYAxis(Min = 0.0, Max = 100.0)
//                                staticChart.ShowChart() |> ignore
//                                staticChart.SaveChartAs("e:\\temp\\pf\\" + image_number.ToString("000") + ".png", ChartImageFormat.Png)
//                                image_number <- image_number + 1
//                                ) |> ignore

    System.Windows.Forms.Application.Run(hostForm);
    0
