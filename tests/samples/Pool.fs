﻿module IntelliFactory.WebSharper.O3D.Samples.Pool

open IntelliFactory.WebSharper.O3D
open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.EcmaScript

type Float2 = float * float
type Float3 = float * float * float
type Float4 = float * float * float * float
type Mat3 = Float3 * Float3 * Float3

type Ball = {
    mutable Mass : float
    mutable AngularInertia : float
    mutable Center : Float3
    mutable Velocity : Float3
    mutable VerticalAcceleration : float
    mutable Orientation : Float4
    mutable AngularVelocity : Float3
    mutable Active : bool
    mutable SunkInPocket : int
}
with
    [<JavaScript>]
    static member Create() = {
        Mass = 1.0
        AngularInertia = 0.4
        Center = (0., 0., 0.)
        Velocity = (0., 0., 0.)
        VerticalAcceleration = 0.
        Orientation = (0., 0., 0., 1.)
        AngularVelocity = (0., 0., 0.)
        Active = true
        SunkInPocket = -1
    }

type BallCollision = {
    i : int
    j : int
    ammt : float
}

type WallCollision = {
    i : int
    x : float
    y : float
    ammt : float
}

type Wall = {
    p : Float2
    q : Float2
    nx : float
    ny : float
    k : float
    a : float
    b : float
}

type CameraPosition = {
    mutable Center : Float3
    mutable theta : float
    mutable phi : float
    mutable radius : float
}
with
    [<JavaScript>]
    static member Create() =
        { Center = (0., 0., 0.)
          theta = 0.
          phi = 0.
          radius = 1. }

type QueueElement = {
    condition : unit -> bool
    action : unit -> unit
}

[<JavaScript>]
let mutable g_clock = 0.
[<JavaScript>]
let mutable g_queueClock = 0.
[<JavaScript>]
let mutable g_shadowOnParams = [||] : O3D.Param<float>[]

type CameraInfo = {
    mutable lastX : float
    mutable lastY : float
    mutable position : CameraPosition
    targetPosition : CameraPosition
    vector_ : Float3
    mutable lerpCoefficient : float
    mutable startingTime : float
}
with
    [<JavaScript>]
    member this.Begin(x, y) =
        this.lastX <- x
        this.lastY <- y

    [<JavaScript>]
    member this.Update(x, y) =
        this.targetPosition.theta <- this.targetPosition.theta - (x - this.lastX) / 200.
        this.targetPosition.phi <- this.targetPosition.phi + (x - this.lastY) / 200.
        this.Bound()
        this.lastX <- x
        this.lastY <- y

    [<JavaScript>]
    member this.Bound() =
        if this.position.phi < 0.01 then
            this.position.phi <- 0.01
        if this.position.phi > System.Math.PI / 2. - 0.01 then
            this.position.phi <- System.Math.PI / 2. - 0.01

    [<JavaScript>]
    member this.GetCurrentPosition() =
        let t = this.lerpCoefficient
        let t = 3. * t * t - 2. * t * t * t
        let a = this.position
        let b = this.targetPosition
        { Center = O3DJS.Math.Add(O3DJS.Math.Mul(1. - t, a.Center),
                                  O3DJS.Math.Mul(t, b.Center))
          radius = (1. - t) * a.radius + t * b.radius
          theta = (1. - t) * a.theta + t * b.theta
          phi = (1. - t) * a.phi + t * b.phi }

    [<JavaScript>]
    member this.GetEyeAndTarget() =
        let p = this.GetCurrentPosition()
        let cosPhi = Math.Cos p.phi
        let target = p.Center
        let eye = O3DJS.Math.Add(target, O3DJS.Math.Mul(p.radius, (cosPhi * Math.Cos p.theta,
                                                                   cosPhi * Math.Sin p.theta,
                                                                   Math.Sin p.phi)))
        (eye, target)

    [<JavaScript>]
    member this.GoTo(center, theta, phi, radius) =
        let center = defaultArg center this.targetPosition.Center
        let theta = defaultArg theta this.targetPosition.theta
        let phi = defaultArg phi this.targetPosition.phi
        let radius = defaultArg radius this.targetPosition.radius
        let p = this.GetCurrentPosition()
        this.position <- p
        this.targetPosition.Center <- center
        this.targetPosition.theta <- theta
        this.targetPosition.phi <- phi
        this.targetPosition.radius <- radius
        this.lerpCoefficient <- 0.
        this.startingTime <- g_clock
        let k = 3. * System.Math.PI / 2.
        let myMod n m = ((n % m) + m) % m
        this.position.theta <-
            myMod (this.position.theta + k) (2.*System.Math.PI) - k
        this.targetPosition.theta <-
            myMod (this.targetPosition.theta + k) (2.*System.Math.PI) - k

    [<JavaScript>]
    member this.BackUp() =
        let c = this.targetPosition.Center
        this.GoTo(Some c, None, Some (System.Math.PI/6.), Some 100.)

    [<JavaScript>]
    member this.ZoomToPoint(center) =
        this.GoTo(Some center, Some this.targetPosition.theta, Some (System.Math.PI/20.), Some 20.)

    [<JavaScript>]
    member this.UpdateClock() =
        this.lerpCoefficient <- min 1. (g_clock - this.startingTime)
        if this.lerpCoefficient = 1. then
            this.position.Center <- this.targetPosition.Center
            this.position.theta <- this.targetPosition.theta
            this.position.phi <- this.targetPosition.phi
            this.position.radius <- this.targetPosition.radius

    [<JavaScript>]
    member this.LookingAt(center) =
        this.targetPosition.Center = center

    [<JavaScript>]
    static member Create() =
        let c = { lastX = 0.
                  lastY = 0.
                  position = CameraPosition.Create()
                  targetPosition = CameraPosition.Create()
                  vector_ = (0., 0., 0.)
                  lerpCoefficient = 1.
                  startingTime = 0. }
        c.GoTo(Some (0., 0., 0.),
               Some (- System.Math.PI / 2.),
               Some (System.Math.PI / 6.),
               Some 140.)
        c

[<JavaScript>]
let g_tableWidth = 45.
[<JavaScript>]
let g_pocketRadius = 2.3
[<JavaScript>]
let g_woodBreadth = 3.2
[<JavaScript>]
let g_tableThickness = 5.
[<JavaScript>]
let g_woodHeight = 1.1
[<JavaScript>]
let g_ballTransforms = Array.init 16 (fun i -> null : O3D.Transform)
[<JavaScript>]
let mutable g_centers = [||] : O3D.Param<Float2>[]

[<JavaScriptType>]
type Physics[<JavaScript>]() =

    [<JavaScript>]
    let record = [||]

    [<JavaScript>]
    let mutable speedFactor = 0.
    [<JavaScript>]
    let maxSpeed = 1.

    [<JavaScript>]
    let pocketCenters =
        let w = g_tableWidth / 2.
        let r = g_pocketRadius
        let root2 = Math.Sqrt 2.
        let x = 0.5 * root2 * r - w
        let y = 0.5 * root2 * r - 2. * w
        [| (w, 0.); (-w, 0.); (x, y); (-x, y); (x, -y); (-x, -y) |]
    [<JavaScript>]
    let left = g_tableWidth / 2. + g_pocketRadius + 1.
    [<JavaScript>]
    let right = g_tableWidth / 2. + g_pocketRadius - 1.
    [<JavaScript>]
    let top = g_tableWidth - g_pocketRadius - 1.
    [<JavaScript>]
    let bottom = - g_tableWidth + g_pocketRadius + 1.

    // The cue ball is slightly heavier than the other ones
    [<JavaScript>]
    let cueWeightRatio = 6. / 5.5
    [<JavaScript>]
    let balls = Array.init 16 (fun i ->
        let ball = Ball.Create()
        if i = 0 then
            ball.Mass <- ball.Mass * cueWeightRatio
            ball.AngularInertia <- ball.AngularInertia * cueWeightRatio
        ball)

    [<JavaScript>]
    let mutable walls = [||]
    [<JavaScript>]
    let mutable collisions = []
    [<JavaScript>]
    let mutable wallCollisions = []

    [<JavaScript>]
    let vectorToQuaternion ((r0, r1, r2) as r) =
        let theta = O3DJS.Math.Length r
        let stot = if theta < 1.0e-6 then 1. else sin(theta/2.) / theta
        (stot * r0, stot * r1, stot * r2, Math.Cos(theta))

    [<JavaScript>]
    member this.Balls = balls

    [<JavaScript>]
    member this.BallOn i =
        this.Balls.[i].Active <- true
        this.Balls.[i].SunkInPocket <- -1
        g_ballTransforms.[i].Visible <- true
        g_shadowOnParams.[i].Value <- 1.

    [<JavaScript>]
    member this.BallOff i =
        this.Balls.[i].Active <- false
        g_ballTransforms.[i].Visible <- false
        g_shadowOnParams.[i].Value <- 0.

    [<JavaScript>]
    member this.PlaceBall(i, ((x, y, _) as p), q) =
        balls.[i].Center <- p
        g_ballTransforms.[i].LocalMatrix <- O3DJS.Math.Matrix4.Translation p
        g_ballTransforms.[i].QuaternionRotate q
        g_centers.[i].Value <- (x, y)

    [<JavaScript>]
    member this.PlaceBall(i, p) =
        this.PlaceBall(i, p, (0., 0., 0., 1.))

    [<JavaScript>]
    member this.PlaceBalls() =
        balls |> Array.iteri (fun i ball ->
            if ball.Active then
                this.PlaceBall(i, ball.Center, ball.Orientation)
            else
                g_shadowOnParams.[i].Value <- 0.)

    [<JavaScript>]
    member this.Step() =
        for i = 0 to 4 do
            this.BallsLoseEnergy()
            this.BallsImpactFloor()
            this.Move(1.)
            while this.Collide() do
                this.Move(-1.)
                this.HandleCollisions()
                this.Move(1.)
            this.Sink()
            this.HandleFalling()
            this.PlaceBalls()

    [<JavaScript>]
    member this.Move(timeStep : float) =
        balls |> Array.iter (fun ball ->
            if ball.Active then
                let (cx, cy, cz) = O3DJS.Math.Add(ball.Center, O3DJS.Math.Mul(timeStep, ball.Velocity))
                ball.Orientation <- O3DJS.Math.Mul(vectorToQuaternion(O3DJS.Math.Mul(timeStep, ball.AngularVelocity)),
                                             ball.Orientation)
                                    |> O3DJS.Quaternions.Normalize
                ball.Center <- (cx, cy, cz + ball.VerticalAcceleration))

    [<JavaScript>]
    member this.ImpartSpeed(i, (dx, dy, _)) =
        let factor = maxSpeed * speedFactor
        let bx, by, bz = balls.[i].Velocity
        balls.[i].Velocity <- (bx + dx*factor, by + dy*factor, bz)

    [<JavaScript>]
    member this.StopAllBalls() =
        balls |> Array.iter (fun ball ->
            ball.Velocity <- (0., 0., 0.)
            ball.AngularVelocity <- (0., 0., 0.))

    [<JavaScript>]
    member this.StopSlowBalls() =
        let epsilon = 0.0001
        balls |> Array.iter (fun ball ->
            if ball.Active then
                if O3DJS.Math.Length ball.Velocity < epsilon then
                    ball.Velocity <- (0., 0., 0.)
                if O3DJS.Math.Length ball.AngularVelocity < epsilon then
                    ball.AngularVelocity <- (0., 0., 0.))

    [<JavaScript>]
    member this.SomeBallsMoving() =
        balls |> Array.exists (fun ball ->
            not ball.Active ||
            (let (v0, v1, v2) = ball.Velocity
             let (w0, w1, w2) = ball.AngularVelocity
             v0 <> 0. || v1 <> 0. || v2 <> 0. ||
             w0 <> 0. || w1 <> 0. || w2 <> 0.))

    [<JavaScript>]
    member this.Sink() =
        balls |> Array.iter (fun ball ->
            if ball.Active then
                let (px, py, _) = ball.Center
                pocketCenters |> Array.iteri (fun j pocketCenter ->
                    if O3DJS.Math.DistanceSquared((px, py), pocketCenter) < g_pocketRadius*g_pocketRadius then
                        ball.VerticalAcceleration <- ball.VerticalAcceleration - 0.005
                        ball.SunkInPocket <- j))

    [<JavaScript>]
    member this.HandleFalling() =
        balls |> Array.iteri (fun i ball ->
            if ball.Active then
                let (px, py, pz) = ball.Center
                if ball.SunkInPocket >= 0 then
                    let pocketCenter = pocketCenters.[ball.SunkInPocket]
                    let (dx, dy) as d = O3DJS.Math.Sub((px, py), pocketCenter)
                    let norm = O3DJS.Math.Length d
                    let maxNorm = g_pocketRadius - Math.Sqrt (max 0. (1. - (pz + 1.) * (pz + 1.)))
                    if norm > maxNorm then
                        let ratio = maxNorm / norm
                        ball.Center <- (px + dx*ratio, py + dy*ratio, pz)
                if pz < -3. then
                    ball.Velocity <- (0., 0., 0.)
                    ball.AngularVelocity <- (0., 0., 0.)
                    ball.VerticalAcceleration <- 0.
                    ball.Active <- false
                    this.BallOff i)

    [<JavaScript>]
    member this.BoundCueBall() =
        let mutable (cx, cy, _) = balls.[0].Center
        if cx < left then
            cx <- left
        if cx > right then
            cx <- right
        if cy < bottom then
            cy <- bottom
        if cy > top then
            cy <- top
        this.PushOut()
        this.PlaceBalls()

    [<JavaScript>]
    member this.Collide() =
        this.CollideBalls()
        // this.CollideWithWalls()
        not (List.isEmpty collisions) && not (List.isEmpty wallCollisions)

    [<JavaScript>]
    member this.PushOut() =
        while this.Collide() do
            // this.PushCollisions()
            ()

    [<JavaScript>]
    member this.CollideBalls() =
        collisions <- []
        balls |> Array.iteri (fun i balli ->
            if balli.Active then
                let (p1x, p1y, _) = balli.Center
                for j = 0 to i - 1 do
                    let ballj = balls.[j]
                    if ballj.Active then
                        let (p2x, p2y, _) = ballj.Center
                        let d2 = O3DJS.Math.DistanceSquared((p1x, p1y), (p2x, p2y))
                        if d2 < 3.99 then
                            let norm = Math.Sqrt d2
                            let collision = {i = i; j = j; ammt = 2. - norm}
                            collisions <- collision :: collisions)

    [<JavaScript>]
    member this.InitWalls() =
        let r = g_pocketRadius
        let w = g_tableWidth
        let path = [| (0., -w/2. +    r, 0.)
                      ( r, -w/2. + 2.*r, 0.)
                      ( r,  w/2. - 2.*r, 0.)
                      (0.,  w/2. -    r, 0.) |]
        let pi = System.Math.PI
        let angles = [| 0.; pi/2.; pi; pi; 3.*pi/2.; 0. |]
        let translations = O3DJS.Math.Mul([|[|-1.; -1.; 0.|]
                                            [| 0.; -2.; 0.|]
                                            [| 1.; -1.; 0.|]
                                            [| 1.;  1.; 0.|]
                                            [| 0.;  2.; 0.|]
                                            [|-1.;  1.; 0.|]|],
                                          [|[|w/2.;   0.; 0.|]
                                            [|  0.; w/2.; 0.|]
                                            [|  0.;   0.; 1.|]|])
        walls <-
            Seq.init 6 (fun i ->
                let newPath = path |> Array.mapi (fun j p ->
                    O3DJS.Math.Matrix4.TransformPoint(
                        O3DJS.Math.Matrix4.Composition(
                            O3DJS.Math.Matrix4.Translation(translations.[i]),
                            O3DJS.Math.Matrix4.RotationZ(angles.[i])),
                        p))
                newPath |> Array.mapi (fun j (px, py, _) ->
                    let (qx, qy, _) =
                        if j = Array.length newPath - 1
                        then (0., 0., 0.)
                        else newPath.[j + 1]
                    let p = (px, py)
                    let q = (qx, qy)
                    let d = O3DJS.Math.Sub(q, p)
                    let (tx, ty) = O3DJS.Math.Normalize d
                    let nx = ty
                    let ny = -tx
                    { p = p; q = q
                      nx = nx; ny = ny
                      k = nx * px + ny * py
                      a = py * nx - px * ny
                      b = qy * nx - qx * ny})
            )
            |> Array.concat

    [<JavaScript>]
    member this.CollideWithWalls(wallList, radius) =
        seq {
            for i = 0 to 15 do
                let ball = balls.[i]
                if ball.Active then
                    let (x, y, _) = ball.Center
                    if not (x > left && x < right &&
                            y > bottom && y < top) then
                        for wall in wallList do
                            let norm = abs (x * wall.nx + y * wall.ny - wall.k)
                            if norm < radius then
                                let t = y * wall.nx - x * wall.ny
                                if t > wall.a && t < wall.b then
                                    yield { i = i; x = wall.nx; y = wall.ny; ammt = 1. - norm }
                                else
                                    let (dx, dy) as d = O3DJS.Math.Sub((x, y), wall.p)
                                    let normSquared = O3DJS.Math.LengthSquared d
                                    if normSquared < radius * radius then
                                        let norm = Math.Sqrt normSquared
                                        yield { i = i; x = dx / norm; y = dy / norm; ammt = 1. - norm }
                                    else
                                        let (dx, dy) as d = O3DJS.Math.Sub((x, y), wall.q)
                                        let normSquared = O3DJS.Math.LengthSquared d
                                        if normSquared < radius * radius then
                                            let norm = Math.Sqrt normSquared
                                            yield { i = i; x = dx / norm; y = dy / norm; ammt = 1. - norm }
        } |> Seq.toList

    [<JavaScript>]
    member this.CollideWithWalls() =
        wallCollisions <- this.CollideWithWalls(walls, 1.)

    [<JavaScript>]
    member this.PushCollisions() =
        wallCollisions |> List.iter (fun c ->
            let (p0, p1, p2) = balls.[c.i].Center
            balls.[c.i].Center <- (p0 + c.ammt*c.x, p1 + c.ammt*c.y, p2))
        collisions |> List.iter (fun c ->
            let pi = balls.[c.i].Center
            let pj = balls.[c.j].Center
            let (dx, dy, _) = O3DJS.Math.Sub(pj, pi)
            let norm = O3DJS.Math.Length((dx, dy))
            let r = (c.ammt * dx / norm / 2., c.ammt * dy / norm / 2., 0.)
            balls.[c.i].Center <- O3DJS.Math.Sub(pi, r)
            balls.[c.j].Center <- O3DJS.Math.Add(pj, r))

    [<JavaScript>]
    member this.HandleCollisions() =
        wallCollisions |> List.iter (fun c ->
            let ball = balls.[c.i]
            let v = ball.Velocity
            let w = ball.AngularVelocity
            let r1 = (-c.x, -c.y, 0.)
            let r2 = (c.x, c.y, 0.)
            let impulse = this.Impulse(v, w, ball.Mass, ball.AngularInertia, r1,
                                       (0., 0., 0.), (0., 0., 0.), 1e100, 1e100, r2,
                                       r1, 0.99, 1., 1.)
            this.ApplyImpulse(c.i, impulse, r1))
        collisions |> List.iter (fun c ->
            let bi = balls.[c.i]
            let bj = balls.[c.j]
            let vi = bi.Velocity
            let wi = bi.AngularVelocity
            let vj = bj.Velocity
            let wj = bj.AngularVelocity
            let ri = O3DJS.Math.Normalize(O3DJS.Math.Sub(bj.Center, bi.Center))
            let rj = O3DJS.Math.Negative(ri)
            let impulse = this.Impulse(vi, wi, bi.Mass, bi.AngularInertia, ri,
                                       vj, wj, bj.Mass, bj.AngularInertia, rj,
                                       ri, 0.99, 0.2, 0.1)
            this.ApplyImpulse(c.i, impulse, ri)
            this.ApplyImpulse(c.j, O3DJS.Math.Negative(impulse), rj))

    [<JavaScript>]
    member this.RandomOrientations() =
        balls |> Array.iter (fun ball ->
            ball.Orientation <- O3DJS.Math.Normalize((Math.Random() - 0.5,
                                                      Math.Random() - 0.5,
                                                      Math.Random() - 0.5,
                                                      Math.Random() - 0.5)))

    // Ensure that z = 0. because unlike the original js,
    // we use the entire vector with eg. Math.Cross
    [<JavaScript>]
    member this.Impulse args =
        let (x, y, _) = this.Impulse' args
        (x, y, 0.)

    [<JavaScript>]
    member this.Impulse'(v1, w1, m1, I1, r1,
                         v2, w2, m2, I2, r2,
                         N, e, u_s, u_d) =
        // Just to be safe, make N unit-length.
        let N = O3DJS.Math.Normalize N

        // Vr is the relative Velocity at the point of impact.
        // Vrn and Vrt are the normal and tangential parts of Vr.
        let Vr = O3DJS.Math.Sub(O3DJS.Math.Add(O3DJS.Math.Cross(w2, r2), v2),
                                O3DJS.Math.Add(O3DJS.Math.Cross(w1, r1), v1))
        let Vrn = O3DJS.Math.Mul(O3DJS.Math.Dot(Vr, N), N)
        let Vrt = O3DJS.Math.Sub(Vr, Vrn)

        let K = O3DJS.Math.Add(this.InertialTensor(m1, I1, r1), this.InertialTensor(m2, I2, r2))
        let Kinverse = O3DJS.Math.Inverse(K)

        // Compute the impulse assuming 0 tangential Velocity.
        let j0 = O3DJS.Math.Mul(Kinverse, O3DJS.Math.Sub(Vr, O3DJS.Math.Mul(-e, Vrn)))

        // If j0 is in the static friction cone, we return that.
        // If the length of Vrt is 0, then we cannot normalize it,
        // so we return j0 in that case, too.
        let j0n = O3DJS.Math.Mul(O3DJS.Math.Dot(j0, N), N)
        let j0t = O3DJS.Math.Sub(j0, j0n)

        if O3DJS.Math.LengthSquared j0t <= u_s * u_s * O3DJS.Math.LengthSquared j0n ||
           O3DJS.Math.LengthSquared Vrt = 0. then
            j0
        else
            // Get a unit-length tangent vector by normalizing the tangent Velocity.
            // The friction impulse acts in the opposite direction.
            let T = O3DJS.Math.Normalize Vrt

            // Compute the current impulse in the normal direction.
            let jn = O3DJS.Math.Dot(O3DJS.Math.Mul(Kinverse, Vr), N)

            // Compute the impulse assuming no friction.
            let js = O3DJS.Math.Mul(Kinverse,
                                    O3DJS.Math.Mul(1. + e, Vrn))

            // Return the frictionless impulse plus the impulse due to friction.
            O3DJS.Math.Add(js, O3DJS.Math.Mul(-u_d * jn, T))

    [<JavaScript>]
    member this.InertialTensor(m, I, r) : Mat3 =
        let (a, b, c) = r

        ((1. / m + (b * b + c * c) / I, (-a * b) / I, (-a * c) / I),
         ((-a * b) / I, 1. / m + (a * a + c * c) / I, (-b * c) / I),
         ((-a * c) / I, (-b * c) / I, 1. / m + (a * a + b * b) / I))

    [<JavaScript>]
    member this.ApplyImpulse(i, impulse : float * float * float, r) =
        let ball = balls.[i]
        let (rx, ry, rz) = r
        ball.Velocity <- O3DJS.Math.Add(ball.Velocity, O3DJS.Math.Div(impulse, ball.Mass))
        ball.AngularVelocity <- O3DJS.Math.Add(ball.AngularVelocity,
                                               O3DJS.Math.Div(O3DJS.Math.Cross(r, impulse),
                                                              ball.AngularInertia))

    [<JavaScript>]
    member this.BallsImpactFloor() =
        balls |> Array.iteri (fun i ball ->
            if ball.Active then
                let (vx, vy, _) = ball.Velocity
                let v = (vx, vy, -0.1)
                let w = ball.AngularVelocity
                let impulse = this.Impulse(v, w, ball.Mass, ball.AngularInertia, (0., 0., -1.),
                                           (0., 0., 0.), (0., 0., 0.), 1e100, 1e100, (0., 0., 1.),
                                           (0., 0., -1.), 0.1, 0.1, 0.02)
                this.ApplyImpulse(i, impulse, (0., 0., -1.)))

    [<JavaScript>]
    member this.BallsLoseEnergy() =
        balls |> Array.iter (fun ball ->
            if ball.Active then
                ball.Velocity <- this.LoseEnergy(ball.Velocity, 0.00004)
                ball.AngularVelocity <- this.LoseEnergy(ball.AngularVelocity, 0.00006))

    [<JavaScript>]
    member this.LoseEnergy(v, epsilon) =
        let vLength = O3DJS.Math.Length v
        if vLength < epsilon then
            (0., 0., 0.)
        else
            let t = epsilon / vLength
            O3DJS.Math.Mul(v, 1. - t)

[<JavaScriptType>]
type Pool [<JavaScript>]() =

    let SHADOWPOV = false
    let RENDER_TARGET_WIDTH = 512
    let RENDER_TARGET_HEIGHT = 512
    let g_light = (0., 0., 50.)
    let mutable g_o3dElement : Dom.Element = null
    let g_cameraInfo = CameraInfo.Create()
    let mutable g_dragging = false
    let mutable g_client : O3D.Client = null
    let mutable g_pack : O3D.Pack = null
    let mutable g_tableRoot : O3D.Transform = null
    let mutable g_shadowRoot : O3D.Transform = null
    let mutable g_hudRoot : O3D.Transform = null
    let mutable g_viewInfo : O3DJS.Rendergraph.ViewInfo = null
    let mutable g_hudViewInfo : O3DJS.Rendergraph.ViewInfo = null
    let mutable g_shadowTexture : O3D.Texture2D = null
    let mutable g_shadowPassViewInfo : O3DJS.Rendergraph.ViewInfo = null
    let mutable g_materials : obj = null
    let mutable g_solidMaterial : O3D.Material = null
    let mutable g_shadowSampler : O3D.Sampler = null
    let mutable g_ballTextures = Array.init 16 (fun _ -> null : O3D.Texture2D)
    let mutable g_ballTextureSamplers = Array.init 16 (fun _ -> null : O3D.Sampler)
    let mutable g_ballTextureSamplerParams = Array.init 16 (fun _ -> null : O3D.Param<O3D.Sampler>)
    let mutable g_barScaling : O3D.Transform = null
    let mutable g_barRoot : O3D.Transform = null
    let mutable g_shooting = false
    let mutable g_rolling = false
    let mutable g_queue : QueueElement[] = [||]
    let mutable g_physics = new Physics()

    [<JavaScript>]
    let SetOptionalParam(material : O3D.Material, name, value : obj) =
        let param = material.GetParam name
        if param <> null then
            param.Value <- value

    [<JavaScript>]
    let UpdateMaterials() =
        JavaScript.ForEach g_materials (fun name ->
            let eye, _ = g_cameraInfo.GetEyeAndTarget()
            SetOptionalParam(JavaScript.Get name g_materials, "eyeWorldPosition", eye)
            false)

    [<JavaScript>]
    let UpdateContext() =
        let perspective = O3DJS.Math.Matrix4.Perspective(O3DJS.Math.DegToRad 30.,
                                                         float g_client.Width / float g_client.Height,
                                                         1., 5000.)
        g_shadowPassViewInfo.DrawContext.Projection <- perspective
        g_viewInfo.DrawContext.Projection <- perspective
        let eye, target = g_cameraInfo.GetEyeAndTarget()
        g_shadowPassViewInfo.DrawContext.View <- O3DJS.Math.Matrix4.LookAt(eye, target, (0., 0., 1.))
        UpdateMaterials()

    [<JavaScript>]
    let StartDragging(e : Dom.MouseEvent) =
        g_cameraInfo.Begin(float e.ClientX, float e.ClientY)
        g_dragging <- true

    [<JavaScript>]
    let Drag(e : Dom.MouseEvent) =
        if g_dragging then
            g_cameraInfo.Update(float e.ClientX, float e.ClientY)
            UpdateContext()

    [<JavaScript>]
    let StopDragging(e : Dom.MouseEvent) =
        if g_dragging then
            g_cameraInfo.Update(float e.ClientX, float e.ClientY)
            UpdateContext()
        g_dragging <- false

    [<JavaScript>]
    let InitPhysics() =
        g_physics.InitWalls()

    [<JavaScript>]
    let InitGlobals(clientElements : Dom.Element[]) =
        g_o3dElement <- clientElements.[0]
        g_client <- JavaScript.Get "client" g_o3dElement
        O3DJS.Base.O3D <- JavaScript.Get "o3d" g_o3dElement
        g_pack <- g_client.CreatePack()

    [<JavaScript>]
    let InitRenderGraph() =
        g_tableRoot <- g_pack.CreateTransform()
        g_tableRoot.Parent <- g_client.Root
        g_shadowRoot <- g_pack.CreateTransform()
        g_shadowRoot.Parent <- g_client.Root
        g_hudRoot <- g_pack.CreateTransform()
        g_hudRoot.Parent <- g_client.Root
        let viewRoot = g_pack.CreateRenderNode()
        viewRoot.Priority <- 1.
        if not SHADOWPOV then
            viewRoot.Parent <- g_client.RenderGraphRoot
        let shadowPassRoot = g_pack.CreateRenderNode()
        shadowPassRoot.Priority <- 0.
        shadowPassRoot.Parent <- g_client.RenderGraphRoot
        g_viewInfo <- O3DJS.Rendergraph.CreateBasicView(g_pack, g_tableRoot, viewRoot, (0., 0., 0., 1.))
        let hudRenderRoot =
            if SHADOWPOV then null
                         else g_client.RenderGraphRoot
        g_hudViewInfo <- O3DJS.Rendergraph.CreateBasicView(g_pack, g_hudRoot, hudRenderRoot)
        g_hudViewInfo.Root.Priority <- g_viewInfo.Root.Priority + 1.
        g_hudViewInfo.ClearBuffer.ClearColorFlag <- false
        g_hudViewInfo.ZOrderedState.GetStateParamCullMode.Value <- O3D.State.Cull.CULL_NONE
        g_hudViewInfo.ZOrderedState.GetStateParamZWriteEnable.Value <- false
        g_hudViewInfo.DrawContext.Projection <- O3DJS.Math.Matrix4.Orthographic(0., 1., 0., 1., -10., 10.)
        g_hudViewInfo.DrawContext.View <- O3DJS.Math.Matrix4.LookAt((0., 0., 1.),
                                                                  (0., 0., 0.),
                                                                  (0., 1., 0.))
        g_shadowTexture <- g_pack.CreateTexture2D(RENDER_TARGET_WIDTH, RENDER_TARGET_HEIGHT,
                                              O3D.Texture.Format.XRGB8, 1, true)
        let renderSurface = g_shadowTexture.GetRenderSurface 0
        let depthSurface = g_pack.CreateDepthStencilSurface(RENDER_TARGET_WIDTH, RENDER_TARGET_HEIGHT)
        let renderSurfaceSet = g_pack.CreateRenderSurfaceSet(RenderSurface = renderSurface,
                                                           RenderDepthStencilSurface = depthSurface,
                                                           Parent = shadowPassRoot)
        let shadowPassParent = if SHADOWPOV then renderSurfaceSet :> O3D.RenderNode
                                            else g_client.RenderGraphRoot
        g_shadowPassViewInfo <- O3DJS.Rendergraph.CreateBasicView(g_pack, g_shadowRoot, shadowPassParent, (0., 0., 0., 1.))
        g_shadowPassViewInfo.ZOrderedState.GetStateParamZComparisonFunction.Value <- O3D.State.Comparison.CMP_ALWAYS

    [<JavaScript>]
    let HandleResizeEvent event =
        UpdateContext()

    let VertexShaderString = JavaScript.Get "value" (Dom.Document.Current.GetElementById "vshader")
    let PixelShaderString = JavaScript.Get "value" (Dom.Document.Current.GetElementById "pshader")

    [<JavaScript>]
    let FinishLoadingBitmaps(bitmaps : O3D.Bitmap[], exn) =
        let bitmap = bitmaps.[0]
        bitmap.FlipVertically()
        let width = bitmap.Width / 4
        let height = bitmap.Height / 4
        let levels = O3DJS.Texture.ComputeNumLevels(width, height)
        for i = 0 to 15 do
            g_ballTextures.[i] <- g_pack.CreateTexture2D(width, height, O3D.Texture.Format.XRGB8, 0, false)
            g_ballTextureSamplers.[i] <- g_pack.CreateSampler(Texture = g_ballTextures.[i])
        for i = 0 to 15 do
            let u = i % 4
            let v = i / 4
            g_ballTextures.[i].DrawImage(bitmap, 0, u * width, v * height, width, height, 0, 0, 0, width, height)
            g_ballTextures.[i].GenerateMips(0, levels - 1)
        for i = 0 to 15 do
            g_ballTextureSamplerParams.[i].Value <- g_ballTextureSamplers.[i]

    [<JavaScript>]
    let InitMaterials() =
        g_materials <-
            [|"solid"; "felt"; "wood"; "cushion"; "billiard"; "ball"; "shadowPlane"|]
            |> Array.fold (fun materials name ->
                let material = g_pack.CreateMaterial()
                let effect = g_pack.CreateEffect()
                let mainString = "void main() { gl_FragColor = " + name + "PixelShader(); }"
                ignore <| effect.LoadVertexShaderFromString VertexShaderString
                ignore <| effect.LoadPixelShaderFromString (PixelShaderString + mainString)
                material.Effect <- effect
                effect.CreateUniformParameters material
                material.DrawList <- g_viewInfo.PerformanceDrawList
                let eye, target = g_cameraInfo.GetEyeAndTarget()
                SetOptionalParam(material, "factor", 2. / g_tableWidth)
                SetOptionalParam(material, "lightWorldPosition", g_light)
                SetOptionalParam(material, "eyeWorldPosition", eye)
                JavaScript.Set materials name material
                materials
               ) (new obj())
        g_solidMaterial <- JavaScript.Get "solid" g_materials
        g_solidMaterial.DrawList <- g_hudViewInfo.ZOrderedDrawList
        (As<O3D.Material> <| JavaScript.Get "shadowPlane" g_materials).DrawList <- g_shadowPassViewInfo.ZOrderedDrawList
        g_shadowSampler <- g_pack.CreateSampler()
        g_shadowSampler.Texture <- g_shadowTexture
        ((As<O3D.Material> <| JavaScript.Get "felt" g_materials).GetParam "textureSampler").Value <- g_shadowSampler
        O3DJS.Io.LoadBitmaps(g_pack,
                             O3DJS.Util.GetAbsoluteURI("../assets/poolballs.png"),
                             FinishLoadingBitmaps) |> ignore

    [<JavaScript>]
    let FlatMesh(material, vertexPositions : Float3[], faceIndices) =
        let vertexInfo = O3DJS.Primitives.CreateVertexInfo()
        let positionStream = vertexInfo.AddStream(3, O3D.Stream.Semantic.POSITION)
        let normalStream = vertexInfo.AddStream(3, O3D.Stream.Semantic.NORMAL)
        faceIndices |> Array.fold (fun vertexCount (faceX, faceY, faceZ, faceW) ->
            let n = O3DJS.Math.Normalize(O3DJS.Math.Cross(O3DJS.Math.Sub(vertexPositions.[faceY],
                                                                         vertexPositions.[faceX]),
                                                          O3DJS.Math.Sub(vertexPositions.[faceZ],
                                                                         vertexPositions.[faceX])))
            let faceFirstIndex = vertexCount
            [|faceX; faceY; faceZ; faceW|] |> Array.iter (fun face_j ->
                let v = vertexPositions.[face_j]
                positionStream.AddElementVector v
                normalStream.AddElementVector n)
            vertexInfo.AddTriangle(0, 1, 2)
            vertexInfo.AddTriangle(0, 2, 3)
            vertexCount + 4) 0
        |> ignore
        vertexInfo.CreateShape(g_pack, material)

    [<JavaScript>]
    let Arc((centerX, centerY), radius, start, end', steps) =
        Array.init steps (fun i ->
            let theta = start + float i * (end' - start) / float steps
            [|centerX + radius * Math.Cos theta; centerY + radius * Math.Sin theta|])


    [<JavaScript>]
    let Flip(a : float[][], b : float[]) =
        let myreverse (l : Float2[]) =
            let n = l.Length
            Array.init n (fun i -> if i = 0 then l.[0] else l.[n - i])
        let r =
            Array.init a.Length (fun i -> (b.[0] * a.[i].[0], b.[1] * a.[i].[1]))
        if b.[0] * b.[1] < 0. then
            myreverse r
        else
            r

    [<JavaScript>]
    let InitShadowPlane() =
        let root = g_pack.CreateTransform(Parent = g_shadowRoot)
        let plane = O3DJS.Primitives.CreatePlane(g_pack,
                                                 As<O3D.Material> (JavaScript.Get "shadowPlane" g_materials),
                                                 g_tableWidth,
                                                 g_tableWidth * 2., 1, 1)
        root.Translate((0., 0., -1.))
        root.RotateX(System.Math.PI / 2.)
        let transforms = Array.init 16 (fun i ->
            let transform = g_pack.CreateTransform(Parent = root)
            transform.AddShape plane
            transform)
        g_centers <- transforms |> Array.map (fun transform ->
            transform.CreateParamFloat2 "ballCenter")
        g_shadowOnParams <- transforms |> Array.map (fun transform ->
            transform.CreateParamFloat("shadowOn", Value = 1.))

    let g_seriousness = 0
    let g_shooting_timers = [||]

    let ComputeShot(i, j,
                    (cx, cy, _ as cueCenter : Float3),
                    (ox, oy, _ as objectCenter : Float3),
                    (px, py, _ as pocketCenter : Float3)) =
        let second = O3DJS.Math.Sub((px, py), (cx, cy))
        let toPocket = O3DJS.Math.Normalize(second)
        let toObject = O3DJS.Math.Normalize((ox - cx, ox - cx))
        let cc = if O3DJS.Math.Dot(toObject, toPocket) > 0.8 then 0.4 else 0.
        () // TODO

    [<JavaScript>]
    let InitTable() =
        let feltMaterial = JavaScript.Get "felt" g_materials
        let woodMaterial = JavaScript.Get "wood" g_materials
        let cushionMaterial = JavaScript.Get "cushion" g_materials
        let billiardMaterial = JavaScript.Get "billiard" g_materials
        let root = g_pack.CreateTransform(Parent = g_tableRoot)
        let g_tableRoot = g_pack.CreateTransform(Parent = root)
        g_tableRoot.Translate(0., 0., -g_tableThickness / 2. - 1.)
        let cushionRoot = g_pack.CreateTransform(Parent = g_tableRoot)
        let ballRoot = g_pack.CreateTransform(Parent = root)
        let root2 = Math.Sqrt 2.
        let scaledPocketRadius = 2. * g_pocketRadius / g_tableWidth
        let scaledWoodBreadth = 2. * g_woodBreadth / g_tableWidth
        let hsrr2 = 0.5 * root2 * scaledPocketRadius
        let felt_polygonA =
            Array.append
                [| [|0.; -2.|]; [|0.; (1. + 0.5*root2) * scaledPocketRadius - 2.|] |]
                (Arc((hsrr2 - 1., hsrr2 - 2.),
                     scaledPocketRadius, 0.5*System.Math.PI, -0.25*System.Math.PI, 15))
        let felt_polygonB =
            Array.append
                [| [|-1.; (1. + 0.5*root2) * scaledPocketRadius - 2.|] |]
                (Arc((hsrr2 - 1., hsrr2 - 2.),
                     scaledPocketRadius, 0.75*System.Math.PI, 0.5*System.Math.PI, 15))
        let felt_polygonC =
            Array.concat [
                [| [|0.; (1. + 0.5*root2) * scaledPocketRadius - 2.|]; [|0.; 0.|] |]
                Arc((-1., 0.), scaledPocketRadius, 0., -0.5 * System.Math.PI, 15)
                [| [|-1.; (1. + 0.5*root2) * scaledPocketRadius - 2.|] |]
            ]
        let wood_polygon =
            Array.concat [
                [| [|-scaledWoodBreadth - 1.; -scaledWoodBreadth - 2.|]
                   [|0.; -scaledWoodBreadth - 2.|]
                   [|0.; -2.|] |]
                Arc((hsrr2 - 1., hsrr2 - 2.),
                    scaledPocketRadius, -0.25*System.Math.PI, -1.25*System.Math.PI, 15)
                Arc((-1., 0.), scaledPocketRadius, 1.5*System.Math.PI, System.Math.PI, 15)
                [| [|-scaledWoodBreadth - 1.; 0.|] |]
            ]
        let m = O3DJS.Math.Mul(g_tableWidth / 2., O3DJS.Math.Identity 2)
        let felt_polygon_A = O3DJS.Math.Mul(felt_polygonA, m)
        let felt_polygon_B = O3DJS.Math.Mul(felt_polygonB, m)
        let felt_polygon_C = O3DJS.Math.Mul(felt_polygonC, m)
        let wood_polygon = O3DJS.Math.Mul(wood_polygon, m)
        let ij = [| [|-1.; -1.|]
                    [|-1.;  1.|]
                    [| 1.; -1.|]
                    [| 1.;  1.|]|]
        let felt_polygons = ij |> Array.map (fun ij -> [|Flip(felt_polygon_A, ij)
                                                         Flip(felt_polygon_B, ij)
                                                         Flip(felt_polygon_C, ij)|])
                               |> Array.concat
        let wood_polygons = ij |> Array.map (fun ij -> Flip(wood_polygon, ij))
        let felt_shapes = felt_polygons |> Array.map (fun poly ->
            O3DJS.Primitives.CreatePrism(g_pack, feltMaterial, poly, g_tableThickness))
        let wood_shapes = wood_polygons |> Array.map (fun poly ->
            O3DJS.Primitives.CreatePrism(g_pack, woodMaterial, poly, g_tableThickness))
        let t = g_pack.CreateTransform(Parent = g_tableRoot)
        felt_shapes |> Array.iter t.AddShape
        wood_shapes |> Array.iter t.AddShape

        let cushionHeight = 1.1 * g_woodHeight
        let cushionUp = g_tableThickness / 2.
        let cushionProp = 0.9 * g_woodHeight
        let cushionDepth = g_tableWidth
        let cushionBreadth = g_pocketRadius
        let cushionSwoop = g_pocketRadius
        let angles = [|0.; System.Math.PI / 2.; System.Math.PI; System.Math.PI; 3. * System.Math.PI / 2.; 0.|]
        let translations = O3DJS.Math.Mul([|[|-1.; -1.; 0.|]
                                            [| 0.; -2.; 0.|]
                                            [| 1.; -1.; 0.|]
                                            [| 1.;  1.; 0.|]
                                            [| 0.;  2.; 0.|]
                                            [|-1.;  1.; 0.|]|],
                                          [|[|g_tableWidth/2.; 0.; 0.|]
                                            [|0.; g_tableWidth/2.; 0.|]
                                            [|0.; 0.; 1.|]|])
        let shortenings = O3DJS.Math.Mul(g_pocketRadius, [|[|   1.; root2|]
                                                           [|root2; root2|]
                                                           [|root2;    1.|]|])
        let billiardThickness = 0.1
        let billiardBreadth = 1.
        let billiardDepth = 0.309
        let billiardOut = -g_woodBreadth/2.
        let billiardSpacing = g_tableWidth/4.

        let billiards =
            [|-1.; 1.|] |> Array.map (fun i ->
                O3DJS.Primitives.CreatePrism(g_pack, billiardMaterial,
                                             [| (billiardOut + billiardBreadth / 2., i * billiardSpacing)
                                                (billiardOut, billiardDepth + i * billiardSpacing)
                                                (billiardOut - billiardBreadth / 2., i * billiardSpacing)
                                                (billiardOut, -billiardDepth + i * billiardSpacing) |],
                                             g_tableThickness + 2. * g_woodHeight + billiardThickness))
        for i = 0 to 5 do
            let backShortening = shortenings.[i%3].[1]
            let frontShortening = shortenings.[i%3].[0]
            let vertexPositions = [| (0., -cushionDepth / 2. + backShortening, cushionUp)
                                     (cushionBreadth, -cushionDepth / 2. + cushionSwoop + backShortening, cushionUp + cushionProp)
                                     (cushionBreadth, -cushionDepth / 2. + cushionSwoop + backShortening, cushionUp + cushionHeight)
                                     (0., -cushionDepth / 2. + backShortening, cushionUp + cushionHeight)
                                     (0., cushionDepth / 2. - frontShortening, cushionUp)
                                     (cushionBreadth, cushionDepth / 2. - cushionSwoop - frontShortening, cushionUp + cushionProp)
                                     (cushionBreadth, cushionDepth / 2. - cushionSwoop - frontShortening, cushionUp + cushionHeight)
                                     (0., cushionDepth / 2. - frontShortening, cushionUp + cushionHeight) |]
            let faceIndices = [| (0, 1, 2, 3)
                                 (7, 6, 5, 4)
                                 (1, 0, 4, 5)
                                 (2, 1, 5, 6)
                                 (3, 2, 6, 7)
                                 (0, 3, 7, 4) |]
            let cushion = FlatMesh(cushionMaterial, vertexPositions, faceIndices)
            let t = g_pack.CreateTransform(LocalMatrix = O3DJS.Math.Mul(O3DJS.Math.Matrix4.RotationZ(angles.[i]),
                                                                        O3DJS.Math.Matrix4.Translation(translations.[i])),
                                         Parent = cushionRoot)
            t.AddShape cushion
            billiards |> Array.iter t.AddShape
            let ball = O3DJS.Primitives.CreateSphere(g_pack, JavaScript.Get "ball" g_materials, 1., 50, 70)
            for i = 0 to 15 do
                let transform = g_pack.CreateTransform(Parent = ballRoot)
                g_ballTextureSamplerParams.[i] <- transform.CreateParamSampler "textureSampler"
                g_ballTransforms.[i] <- transform
                transform.AddShape ball

    [<JavaScript>]
    let InitHud() =
        let barT1 = g_pack.CreateTransform(Parent = g_hudRoot)
        g_barScaling <- g_pack.CreateTransform(Parent = barT1)
        let barT2 = g_pack.CreateTransform(Parent = g_barScaling)
        let backT2 = g_pack.CreateTransform(Parent = barT1)
        g_barRoot <- barT1
        let plane = O3DJS.Primitives.CreatePlane(g_pack, g_solidMaterial, 1., 1., 1, 1,
                                                 ((1., 0., 0., 0.),
                                                  (0., 0., 1., 0.),
                                                  (0.,-1., 0., 0.),
                                                  (0., 0., 0., 1.)))
        let backPlane = O3DJS.Primitives.CreatePlane(g_pack, g_solidMaterial, 1., 1., 1, 1,
                                                     ((1., 0., 0., 0.),
                                                      (0., 0., 1., 0.),
                                                      (0.,-1., 0., 0.),
                                                      (0., 0., 0., 1.)))
        barT2.AddShape(plane)
        //backT2.AddShape(backPlane)
        barT1.Translate((0.05, 0.05, 0.))
        barT1.Scale((0.05, 0.9, 1.))
        g_barScaling.LocalMatrix <- O3DJS.Math.Matrix4.Scaling((1., 0., 1.))
        barT2.Translate((0.5, 0.5, 0.))
        backT2.Translate((0.5, 0.5, 0.1))

    [<JavaScript>]
    let SetBarScale t =
        g_barScaling.LocalMatrix <- O3DJS.Math.Matrix4.Scaling((1., t, 1.))

    [<JavaScript>]
    let Onrender (event : O3D.RenderEvent) =
        g_clock <- g_clock + event.ElapsedTime
        g_queueClock <- g_queueClock + event.ElapsedTime
        let clock = g_queueClock
        if g_queue.Length > 0 && g_queue.[0].condition() then
            let action = g_queue.[0].action
            g_queue <- Array.sub g_queue 1 (g_queue.Length - 1)
            action()
            g_queueClock <- 0.
        g_cameraInfo.UpdateClock()
        if g_physics.SomeBallsMoving() then
            g_physics.Step()
            g_physics.StopSlowBalls()
        elif g_rolling then
            g_rolling <- false
            let cueBall = g_physics.Balls.[0]
            if g_cameraInfo.LookingAt cueBall.Center then
                g_barRoot.Visible <- true
            if not cueBall.Active then
                g_physics.BallOn 0
                cueBall.Center <- (0., 0., 0.)
                g_physics.BoundCueBall()
        UpdateContext()

    [<JavaScript>]
    let KeyPressed() =
        ()

    [<JavaScript>]
    let KeyUp() =
        ()

    [<JavaScript>]
    let KeyDown() =
        ()

    [<JavaScript>]
    let ScrollWheel() =
        ()

    [<JavaScript>]
    let Rack(game) =
        let root3 = Math.Sqrt 3.
        let yOffset = 6. * g_tableWidth / 12.
        let cueYOffset = - g_tableWidth / 2.
        for i = 0 to 15 do
            g_physics.BallOn i
        g_physics.StopAllBalls()
        match game with
        | 8 ->
            g_physics.PlaceBall(1, (0., 0. + yOffset, 0.))
            g_physics.PlaceBall(9, (-1., root3 + yOffset, 0.))
            g_physics.PlaceBall(2, (1., root3 + yOffset, 0.))
            g_physics.PlaceBall(10, (2., 2. * root3 + yOffset, 0.))
            g_physics.PlaceBall(8, (0., 2. * root3 + yOffset, 0.))
            g_physics.PlaceBall(3, (-2., 2. * root3 + yOffset, 0.))
            g_physics.PlaceBall(11, (-3., 3. * root3 + yOffset, 0.))
            g_physics.PlaceBall(4, (-1., 3. * root3 + yOffset, 0.))
            g_physics.PlaceBall(12, (1., 3. * root3 + yOffset, 0.))
            g_physics.PlaceBall(5, (3., 3. * root3 + yOffset, 0.))
            g_physics.PlaceBall(13, (4., 4. * root3 + yOffset, 0.))
            g_physics.PlaceBall(6, (2., 4. * root3 + yOffset, 0.))
            g_physics.PlaceBall(14, (0., 4. * root3 + yOffset, 0.))
            g_physics.PlaceBall(15, (-2., 4. * root3 + yOffset, 0.))
            g_physics.PlaceBall(7, (-4., 4. * root3 + yOffset, 0.))
            g_physics.PlaceBall(0, (0., cueYOffset, 0.))
        | 9 ->
            g_physics.PlaceBall(1, (0., 0. + yOffset, 0.))
            g_physics.PlaceBall(2, (1., root3 + yOffset, 0.))
            g_physics.PlaceBall(3, (-1., root3 + yOffset, 0.))
            g_physics.PlaceBall(9, (0., 2. * root3 + yOffset, 0.))
            g_physics.PlaceBall(4, (2., 2. * root3 + yOffset, 0.))
            g_physics.PlaceBall(5, (-2., 2. * root3 + yOffset, 0.))
            g_physics.PlaceBall(6, (1., 3. * root3 + yOffset, 0.))
            g_physics.PlaceBall(7, (-1., 3. * root3 + yOffset, 0.))
            g_physics.PlaceBall(8, (0., 4. * root3 + yOffset, 0.))
            for i = 10 to 15 do
                g_physics.PlaceBall(i, (0., 0., -5.))
                g_physics.BallOff i
            g_physics.PlaceBall(0, (0., cueYOffset, 0.))
        | 0 ->
            for i = 1 to 15 do
                g_physics.PlaceBall(i, (0., 0., -5.))
                g_physics.BallOff i
            g_physics.PlaceBall(0, (0., 0., cueYOffset))
        | 1 ->
            for i = 1 to 15 do
                g_physics.PlaceBall(i, (0., 0., -5.))
                g_physics.BallOff i
            g_physics.PlaceBall(0, (0., cueYOffset, 0.))
            g_physics.PlaceBall(1, (-g_tableWidth/4., cueYOffset/2., 0.))
            g_physics.PlaceBall(2, (-3.*g_tableWidth/8., cueYOffset/4., 0.))
            g_physics.PlaceBall(3, (g_tableWidth/4., 0., 0.))
            g_physics.BallOn 0
            g_physics.BallOn 1
            g_physics.BallOn 2
            g_physics.BallOn 3
        g_physics.RandomOrientations()
        g_physics.PlaceBalls()
        g_cameraInfo.GoTo(Some (0., 0., 0.), Some (-System.Math.PI/2.), Some (System.Math.PI/6.), Some 140.)

    [<JavaScript>]
    let SetRenderCallback() =
        g_client.SetRenderCallback Onrender

    [<JavaScript>]
    let RegisterEventCallbacks() =
        O3DJS.Event.AddEventListener(g_o3dElement, "mousedown", StartDragging)
        O3DJS.Event.AddEventListener(g_o3dElement, "mousemove", Drag)
        O3DJS.Event.AddEventListener(g_o3dElement, "mouseup", StopDragging)
        O3DJS.Event.AddEventListener(g_o3dElement, "keypress", KeyPressed)
        O3DJS.Event.AddEventListener(g_o3dElement, "keyup", KeyUp)
        O3DJS.Event.AddEventListener(g_o3dElement, "keydown", KeyDown)
        O3DJS.Event.AddEventListener(g_o3dElement, "wheel", ScrollWheel)

    [<JavaScript>]
    let Main(clientElements) =
        InitPhysics()
        InitGlobals(clientElements)
        InitRenderGraph()
        UpdateContext()
        InitMaterials()
        InitShadowPlane()
        InitTable()
        InitHud()
        Rack(8)
        SetRenderCallback()
        RegisterEventCallbacks()

    [<JavaScript>]
    member this.CueNewShot(power) =
        g_queue <- [||]

    [<JavaScript>]
    member this.InitClient() =
        g_queue <- [|{ condition = fun () -> not (g_shooting || g_rolling)
                       action = fun () -> this.CueNewShot 0.9 }|]
        O3DJS.Webgl.MakeClients(Main)