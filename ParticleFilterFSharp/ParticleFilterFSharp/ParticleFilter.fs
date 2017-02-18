namespace ParticleFilter

open System;

open Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols;

open MathNet.Numerics.Random;
open MathNet.Numerics.Distributions;

module ParticleFilter =
    
    type StateVector = { x : float<m> ; y : float<m> ; orientation : float }

    type ControlVector = { move : float<m> ; rotate : float }

    type World = { robot : StateVector; particleStates : StateVector list }

    type Noise = { sense : float ; forward : float ; turn : float }

    type UpdateParams = { control : ControlVector ; markers : seq<float<m> * float<m>> ; noise : Noise }

    let create_random_particle (worldsize:float) (random:System.Random) =
        let x = random.NextDouble() * worldsize
        let y = random.NextDouble() * worldsize
        let orientation = random.NextDouble() * 2.0 * System.Math.PI;
        { x = x * 1.0<m>; y = y * 1.0<m>; orientation = orientation }

    let create_particles worldsize count (random:System.Random) =
        let create_random_particle' = fun () -> create_random_particle worldsize random
        List.init count (fun _ -> create_random_particle'())

    let gauss mean sigma random = Normal.Sample(random, mean, sigma)

    let distance_from_landmark robotState (landmark : (float<m> * float<m>)) = 
        let (x, y) = landmark;
        sqrt(((float(robotState.x - x)) ** 2.0) + (float(robotState.y - y)) ** 2.0)

    let sense senseNoise (landmarks:seq<float<m> * float<m>>) random (robotState:StateVector) =
        landmarks |> Seq.map(fun landmark ->
                                 let dist = distance_from_landmark robotState landmark
                                 dist + (gauss 0.0 senseNoise random))
                  |> Seq.toArray

    let gaussian mu sigma x =
        exp(- ((mu - x) ** 2.0) / (sigma ** 2.0) / 2.0) / sqrt(2.0 * Math.PI * (sigma ** 2.0))

    let measurement_prob (measurement:float[]) (landmarks:seq<float<m> * float<m>>) sense_noise state =
        landmarks |> Seq.zip measurement
                  |> Seq.fold (fun current_prob (measurement, landmark) ->
                                   let dist = distance_from_landmark state landmark
                                   current_prob * gaussian dist sense_noise measurement) 1.0

    // also called stochastic universal sampling?
    // search online for fast perfect weighted resampling algorithm paper
    let inline weighted_resample (random:System.Random) (input: array<(float * ^a)>) =
        let inputLength = input.Length
        let mw = input |> Seq.maxBy (fun (weight, value) -> weight) |> fst
        let mutable index = random.Next(0, inputLength)
        let mutable beta = 0.0

        seq {
            for i = 0 to inputLength do
                beta <- beta + random.NextDouble() * 2.0 * mw
                while beta > fst input.[index] do
                    beta <- beta - fst input.[index]
                    index <- (index + 1) % inputLength
                yield snd input.[index]
            }
        |> Seq.toArray

    let update_state forward_noise turn_noise random control state =
        let orientation = state.orientation + control.rotate + (random |> gauss 0.0 turn_noise);
        let wrappedOrientation = if orientation >= 2.0 * Math.PI then orientation - (2.0 * Math.PI) else orientation

        let dist = control.move + ((random |> gauss 0.0 forward_noise) * 1.0<m>)
        let x = state.x + (cos(wrappedOrientation) * dist)
        let y = state.y + (sin(wrappedOrientation) * dist)
        let wrappedX = if x < 0.0<m> then (x + 100.0<m>) else if x > 100.0<m> then (x - 100.0<m>) else x
        let wrappedY = if y < 0.0<m> then (y + 100.0<m>) else if y > 100.0<m> then (y - 100.0<m>) else y
        { x = wrappedX; y = wrappedY; orientation = wrappedOrientation }

    let update (random : System.Random) (updateParams : UpdateParams) {robot = r;  particleStates = p } =
        let updateParticleState = update_state updateParams.noise.forward updateParams.noise.turn random updateParams.control

        let new_r = r |> update_state 0.0 0.0 random updateParams.control
        let measurement = sense updateParams.noise.sense updateParams.markers random new_r

        let new_p = p |> List.map (fun p -> p |> updateParticleState)
                      |> List.map (fun p -> (measurement_prob measurement updateParams.markers updateParams.noise.sense p, p))
                      |> List.toArray 
                      |> (weighted_resample random)

        { robot = new_r ; particleStates = new_p |> Array.toList }
