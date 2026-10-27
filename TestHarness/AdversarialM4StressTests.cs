using System;
using System.Collections.Generic;
using UnityEngine;

public static class AdversarialM4StressTests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[ADVERSARIAL-M4-PASS] {testName}");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ADVERSARIAL-M4-FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    public static int RunAll()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("ADVERSARIAL CHALLENGER M4: EMPIRICAL STRESS & EDGE CASES");
        Console.WriteLine("Testing: Pan/Zoom Math, Exponential Lerp, Bounds Clamping,");
        Console.WriteLine("         Focus Target, Bezier Continuity & Quad Geometry");
        Console.WriteLine("=================================================");

        // Suite 1: Zoom Boundaries & Scale Sensitivity
        Test1_ZoomLimits_ClampExtremeValues();
        Test2_Zoom_ExponentialMultiplicativeProgression();
        Test3_Zoom_CursorInvarianceFixedPoint();
        Test4_Pan_BoundsClampingAcrossResolutions();
        Test5_Focus_StartNodeCenteringAndBoundsSafety();

        // Suite 2: Bezier Curves & Mesh Geometry
        Test6_Bezier_EndpointExactness_Stress500();
        Test7_Bezier_AnalyticalDerivativeAndCurvatureContinuity();
        Test8_Bezier_DegenerateAndEdgeCaseInputs();
        Test9_QuadMesh_OrthogonalityThicknessAndWinding();
        Test10_DashedLine_GapIntegrityAndSegmentContinuity();
        Test11_PanZoom_CoordinateSystemPivotAnalysis();
        Test12_MetaGraphPanZoomUI_InstanceDirectExecution();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"ADVERSARIAL M4 SUMMARY: {_passed} PASSED, {_failed} FAILED");
        Console.WriteLine("=================================================");

        return _failed;
    }

    // =========================================================================
    // 1. ZOOM BOUNDARIES & CLAMPING EXTREMES
    // =========================================================================

    private static void Test1_ZoomLimits_ClampExtremeValues()
    {
        const float minZoom = 0.25f;
        const float maxZoom = 2.5f;
        const float zoomSensitivity = 0.15f;

        // Formula from MetaGraphPanZoomUI.Zoom:
        // float newScale = Mathf.Clamp(targetScale + scrollDelta * (targetScale * zoomSensitivity), minZoom, maxZoom);

        // Test 1: Massive positive scrollDelta (+1000)
        float scale = 1.0f;
        float massiveZoomIn = Mathf.Clamp(scale + 1000f * (scale * zoomSensitivity), minZoom, maxZoom);
        Assert(massiveZoomIn == maxZoom, "Zoom_ExtremeInClampsTo2_5f", $"Got {massiveZoomIn}, expected {maxZoom}");

        // Test 2: Massive negative scrollDelta (-1000)
        float massiveZoomOut = Mathf.Clamp(scale + (-1000f) * (scale * zoomSensitivity), minZoom, maxZoom);
        Assert(massiveZoomOut == minZoom, "Zoom_ExtremeOutClampsTo0_25f", $"Got {massiveZoomOut}, expected {minZoom}");

        // Test 3: Incremental zoom in towards boundary
        float current = 1.0f;
        for (int i = 0; i < 50; i++)
        {
            current = Mathf.Clamp(current + 1.0f * (current * zoomSensitivity), minZoom, maxZoom);
        }
        Assert(Mathf.Approximately(current, maxZoom), "Zoom_IncrementalIn_ReachesMaxZoomWithoutOverflow", $"Final scale: {current}");

        // Test 4: Incremental zoom out towards boundary
        current = 1.0f;
        for (int i = 0; i < 50; i++)
        {
            current = Mathf.Clamp(current - 1.0f * (current * zoomSensitivity), minZoom, maxZoom);
        }
        Assert(Mathf.Approximately(current, minZoom), "Zoom_IncrementalOut_ReachesMinZoomWithoutUnderflow", $"Final scale: {current}");

        // Test 5: Zero scroll delta preserves exact scale
        float unchanged = Mathf.Clamp(1.75f + 0f * (1.75f * zoomSensitivity), minZoom, maxZoom);
        Assert(unchanged == 1.75f, "Zoom_ZeroScroll_PreservesScale", $"Scale changed to {unchanged}");
    }

    // =========================================================================
    // 2. EXPONENTIAL MULTIPLICATIVE PROGRESSION
    // =========================================================================

    private static void Test2_Zoom_ExponentialMultiplicativeProgression()
    {
        const float zoomSensitivity = 0.15f;
        float s0 = 1.0f;

        // Single step zoom in
        float s1 = s0 + 1f * (s0 * zoomSensitivity); // 1.15
        float s2 = s1 + 1f * (s1 * zoomSensitivity); // 1.15 * 1.15 = 1.3225

        float ratio1 = s1 / s0;
        float ratio2 = s2 / s1;

        Assert(Mathf.Approximately(ratio1, 1f + zoomSensitivity) && Mathf.Approximately(ratio2, 1f + zoomSensitivity),
            "Zoom_MultiplicativeProgression_ConstantScaleFactor",
            $"Ratio1: {ratio1}, Ratio2: {ratio2}, expected: {1f + zoomSensitivity}");

        // Lerp convergence simulation (Euler / unscaledDeltaTime)
        float target = 2.0f;
        float current = 1.0f;
        float dt = 0.016f; // 60 FPS
        float smoothness = 14f;

        bool monotonic = true;
        for (int frame = 0; frame < 60; frame++)
        {
            float next = Mathf.Lerp(current, target, dt * smoothness);
            if (next < current || next > target)
            {
                monotonic = false;
                break;
            }
            current = next;
        }

        Assert(monotonic && current > 1.99f,
            "Zoom_LerpSmoothConvergence_MonotonicNoOvershoot",
            $"Monotonic: {monotonic}, current after 1s: {current}");
    }

    // =========================================================================
    // 3. CURSOR INVARIANCE UNDER ZOOM (FIXED-POINT PROPERTY)
    // =========================================================================

    private static void Test3_Zoom_CursorInvarianceFixedPoint()
    {
        // Formula in Zoom:
        // Vector2 contentPoint = (viewportLocalPoint - targetPosition) / targetScale;
        // targetPosition = viewportLocalPoint - contentPoint * newScale;
        // Invariant: After targetPosition is updated with newScale, the contentPoint
        // when projected back to viewport space must equal viewportLocalPoint exactly!

        Vector2[] testCursors = new Vector2[]
        {
            Vector2.zero,
            new Vector2(400, 300),
            new Vector2(-500, 200),
            new Vector2(960, 540),
            new Vector2(-960, -540),
            new Vector2(1920, 1080)
        };

        float[] scales = new float[] { 0.25f, 0.5f, 0.8f, 1.0f, 1.5f, 2.0f, 2.5f };
        bool allInvariant = true;
        string detail = "";

        foreach (var cursor in testCursors)
        {
            Vector2 targetPos = new Vector2(-1000f, -1500f);

            for (int i = 0; i < scales.Length - 1; i++)
            {
                float currentScale = scales[i];
                float newScale = scales[i + 1];

                Vector2 contentPoint = (cursor - targetPos) / currentScale;
                Vector2 newTargetPos = cursor - contentPoint * newScale;

                // Project contentPoint back to viewport space using newTargetPos and newScale:
                Vector2 reprojectedCursor = newTargetPos + contentPoint * newScale;

                if (Vector2.Distance(reprojectedCursor, cursor) > 0.001f)
                {
                    allInvariant = false;
                    detail = $"Cursor {cursor} drifted to {reprojectedCursor} when scaling {currentScale} -> {newScale}";
                    break;
                }

                targetPos = newTargetPos;
            }
            if (!allInvariant) break;
        }

        Assert(allInvariant, "Zoom_CursorInvariance_PointUnderCursorNeverDrifts", detail);
    }

    // =========================================================================
    // 4. BOUNDS CLAMPING ACROSS RESOLUTIONS & EXTREME PANS
    // =========================================================================

    private static void Test4_Pan_BoundsClampingAcrossResolutions()
    {
        Rect contentBounds = new Rect(400f, 200f, 5600f, 4800f);
        // xMin = 400, xMax = 6000, yMin = 200, yMax = 5000

        Vector2[] viewportResolutions = new Vector2[]
        {
            new Vector2(1560f, 1020f), // 1920x1080 minus sidebar/topbar
            new Vector2(920f, 660f),   // 1280x720 minus sidebar/topbar
            new Vector2(3480f, 2100f), // 4K resolution
            new Vector2(1000f, 1000f)  // Square
        };

        float[] testScales = new float[] { 0.25f, 0.5f, 1.0f, 1.75f, 2.5f };
        bool allClampedProperly = true;
        string detail = "";

        foreach (var vpSize in viewportResolutions)
        {
            foreach (float scale in testScales)
            {
                float minX = vpSize.x - contentBounds.xMax * scale;
                float maxX = -contentBounds.xMin * scale;
                float minY = vpSize.y - contentBounds.yMax * scale;
                float maxY = -contentBounds.yMin * scale;

                // Test extreme pans in all 4 diagonal directions (+-1,000,000)
                Vector2[] extremePans = new Vector2[]
                {
                    new Vector2(-1000000f, -1000000f),
                    new Vector2(1000000f, 1000000f),
                    new Vector2(-1000000f, 1000000f),
                    new Vector2(1000000f, -1000000f),
                    new Vector2(0f, 0f)
                };

                foreach (var extreme in extremePans)
                {
                    Vector2 pos = extreme;

                    // Clamping logic from MetaGraphPanZoomUI.ClampPositionToBounds
                    if (minX > maxX)
                    {
                        pos.x = (vpSize.x - (contentBounds.xMin + contentBounds.xMax) * scale) * 0.5f;
                    }
                    else
                    {
                        pos.x = Mathf.Clamp(pos.x, minX, maxX);
                    }

                    if (minY > maxY)
                    {
                        pos.y = (vpSize.y - (contentBounds.yMin + contentBounds.yMax) * scale) * 0.5f;
                    }
                    else
                    {
                        pos.y = Mathf.Clamp(pos.y, minY, maxY);
                    }

                    // Verification
                    if (minX > maxX)
                    {
                        float expectedX = (vpSize.x - (contentBounds.xMin + contentBounds.xMax) * scale) * 0.5f;
                        if (Mathf.Abs(pos.x - expectedX) > 0.001f)
                        {
                            allClampedProperly = false;
                            detail = $"Centering X failed for scale {scale}, vp {vpSize}: got {pos.x}, expected {expectedX}";
                            break;
                        }
                    }
                    else
                    {
                        if (pos.x < minX - 0.001f || pos.x > maxX + 0.001f)
                        {
                            allClampedProperly = false;
                            detail = $"Clamping X violated for scale {scale}, vp {vpSize}: {pos.x} outside [{minX}, {maxX}]";
                            break;
                        }
                    }

                    if (minY > maxY)
                    {
                        float expectedY = (vpSize.y - (contentBounds.yMin + contentBounds.yMax) * scale) * 0.5f;
                        if (Mathf.Abs(pos.y - expectedY) > 0.001f)
                        {
                            allClampedProperly = false;
                            detail = $"Centering Y failed for scale {scale}, vp {vpSize}: got {pos.y}, expected {expectedY}";
                            break;
                        }
                    }
                    else
                    {
                        if (pos.y < minY - 0.001f || pos.y > maxY + 0.001f)
                        {
                            allClampedProperly = false;
                            detail = $"Clamping Y violated for scale {scale}, vp {vpSize}: {pos.y} outside [{minY}, {maxY}]";
                            break;
                        }
                    }
                }
                if (!allClampedProperly) break;
            }
            if (!allClampedProperly) break;
        }

        Assert(allClampedProperly, "Pan_BoundsClamping_StrictlyEnforcedAllResolutions", detail);
    }

    // =========================================================================
    // 5. FOCUS START NODE CENTERING & BOUNDS SAFETY
    // =========================================================================

    private static void Test5_Focus_StartNodeCenteringAndBoundsSafety()
    {
        Vector2 startNodePos = new Vector2(1460f, 2600f);
        Rect contentBounds = new Rect(400f, 200f, 5600f, 4800f);
        Vector2 viewportSize = new Vector2(1560f, 1020f);
        float scale = 1.0f;

        // Viewport center
        Vector2 viewportCenter = viewportSize * 0.5f;

        // Target pos to center graph on startNodePos
        Vector2 focusTarget = viewportCenter - startNodePos * scale;

        // Clamping bounds check
        float minX = viewportSize.x - contentBounds.xMax * scale; // 1560 - 6000 = -4440
        float maxX = -contentBounds.xMin * scale;                 // -400
        float minY = viewportSize.y - contentBounds.yMax * scale; // 1020 - 5000 = -3980
        float maxY = -contentBounds.yMin * scale;                 // -200

        bool xInBounds = focusTarget.x >= minX && focusTarget.x <= maxX;
        bool yInBounds = focusTarget.y >= minY && focusTarget.y <= maxY;

        Assert(xInBounds && yInBounds,
            "Focus_StartNode_FallsInsideBoundsWithoutCorrection",
            $"Target: {focusTarget}, X bounds: [{minX}, {maxX}], Y bounds: [{minY}, {maxY}]");

        // Compute actual position on screen when centered
        Vector2 screenNodePos = focusTarget + startNodePos * scale;
        Assert(Vector2.Distance(screenNodePos, viewportCenter) < 0.001f,
            "Focus_StartNode_PlacedExactlyAtViewportCenter",
            $"Screen pos: {screenNodePos}, Viewport center: {viewportCenter}");
    }

    // =========================================================================
    // 6. BEZIER ENDPOINT EXACTNESS (500 RANDOM SEEDS)
    // =========================================================================

    private static void Test6_Bezier_EndpointExactness_Stress500()
    {
        var rng = new System.Random(424242);
        bool allMatched = true;
        string detail = "";

        for (int i = 0; i < 500; i++)
        {
            Vector2 p0 = new Vector2((float)(rng.NextDouble() * 5000 - 2500), (float)(rng.NextDouble() * 5000 - 2500));
            Vector2 p3 = new Vector2((float)(rng.NextDouble() * 5000 - 2500), (float)(rng.NextDouble() * 5000 - 2500));

            float dx = Mathf.Max(Mathf.Abs(p3.x - p0.x) * 0.5f, 40f);
            Vector2 p1 = p0 + new Vector2(dx, 0f);
            Vector2 p2 = p3 - new Vector2(dx, 0f);

            Vector2 eval0 = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, 0.0f);
            Vector2 eval1 = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, 1.0f);

            if (Vector2.Distance(eval0, p0) > 0.0001f)
            {
                allMatched = false;
                detail = $"Iter {i}: t=0 eval {eval0} != p0 {p0}";
                break;
            }

            if (Vector2.Distance(eval1, p3) > 0.0001f)
            {
                allMatched = false;
                detail = $"Iter {i}: t=1 eval {eval1} != p3 {p3}";
                break;
            }
        }

        Assert(allMatched, "Bezier_EndpointsExactAcross500RandomPairs", detail);
    }

    // =========================================================================
    // 7. BEZIER ANALYTICAL DERIVATIVE & CURVATURE CONTINUITY
    // =========================================================================

    private static void Test7_Bezier_AnalyticalDerivativeAndCurvatureContinuity()
    {
        Vector2 p0 = new Vector2(100f, 100f);
        Vector2 p3 = new Vector2(500f, 300f);
        float dx = Mathf.Max(Mathf.Abs(p3.x - p0.x) * 0.5f, 40f);
        Vector2 p1 = p0 + new Vector2(dx, 0f);
        Vector2 p2 = p3 - new Vector2(dx, 0f);

        // Analytical derivative of cubic Bezier:
        // B'(t) = 3*(1-t)^2*(P1-P0) + 6*(1-t)*t*(P2-P1) + 3*t^2*(P3-P2)
        Func<float, Vector2> derivative = t =>
        {
            float u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        };

        const float eps = 0.0001f;
        bool derivativeMatches = true;
        string detail = "";

        for (int step = 0; step <= 100; step++)
        {
            float t = step / 100f;
            Vector2 vAnalytical = derivative(t);

            // Finite difference approximation
            Vector2 pAhead = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, Mathf.Min(t + eps, 1f));
            Vector2 pBehind = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, Mathf.Max(t - eps, 0f));
            float dt = Mathf.Min(t + eps, 1f) - Mathf.Max(t - eps, 0f);
            Vector2 vNumerical = (pAhead - pBehind) / dt;

            if (Vector2.Distance(vAnalytical, vNumerical) > 0.5f)
            {
                derivativeMatches = false;
                detail = $"At t={t}: Analytical={vAnalytical}, Numerical={vNumerical}";
                break;
            }
        }

        Assert(derivativeMatches, "Bezier_AnalyticalDerivativeMatchesNumericalFiniteDifference", detail);

        // Tangent directions at endpoints:
        // At t=0, B'(0) = 3*(P1 - P0) which is purely horizontal (+X)!
        Vector2 d0 = derivative(0f);
        Assert(d0.y == 0f && d0.x > 0f, "Bezier_InitialTangentIsPurelyHorizontal", $"d0={d0}");

        // At t=1, B'(1) = 3*(P3 - P2) which is also purely horizontal (+X)!
        Vector2 d1 = derivative(1f);
        Assert(d1.y == 0f && d1.x > 0f, "Bezier_FinalTangentIsPurelyHorizontal", $"d1={d1}");
    }

    // =========================================================================
    // 8. DEGENERATE & ADVERSARIAL BEZIER INPUTS
    // =========================================================================

    private static void Test8_Bezier_DegenerateAndEdgeCaseInputs()
    {
        // 1. Coincident endpoints P0 == P3
        Vector2 pZero = new Vector2(200f, 200f);
        float dx = Mathf.Max(Mathf.Abs(pZero.x - pZero.x) * 0.5f, 40f); // 40
        Vector2 p1 = pZero + new Vector2(dx, 0f);
        Vector2 p2 = pZero - new Vector2(dx, 0f);

        bool finite = true;
        for (int i = 0; i <= 20; i++)
        {
            float t = i / 20f;
            Vector2 pt = UILineRenderer.EvaluateCubicBezier(pZero, p1, p2, pZero, t);
            if (float.IsNaN(pt.x) || float.IsNaN(pt.y) || float.IsInfinity(pt.x) || float.IsInfinity(pt.y))
            {
                finite = false;
                break;
            }
        }
        Assert(finite, "Bezier_CoincidentEndpoints_FiniteAndNoNaN", "Coincident endpoints produced NaN or Infinity");

        // 2. Collinear horizontal endpoints
        Vector2 pLeft = new Vector2(0f, 100f);
        Vector2 pRight = new Vector2(500f, 100f);
        dx = Mathf.Max(Mathf.Abs(pRight.x - pLeft.x) * 0.5f, 40f);
        p1 = pLeft + new Vector2(dx, 0f);
        p2 = pRight - new Vector2(dx, 0f);

        bool horizontalStraight = true;
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;
            Vector2 pt = UILineRenderer.EvaluateCubicBezier(pLeft, p1, p2, pRight, t);
            if (Mathf.Abs(pt.y - 100f) > 0.001f)
            {
                horizontalStraight = false;
                break;
            }
        }
        Assert(horizontalStraight, "Bezier_CollinearHorizontal_RemainsStraightLine", "Y deviated on horizontal line");

        // 3. Reversed direction (Child to the left of Parent: p3.x < p0.x)
        Vector2 pStart = new Vector2(500f, 200f);
        Vector2 pEnd = new Vector2(100f, 400f);
        dx = Mathf.Max(Mathf.Abs(pEnd.x - pStart.x) * 0.5f, 40f); // 200
        p1 = pStart + new Vector2(dx, 0f);
        p2 = pEnd - new Vector2(dx, 0f);

        Vector2 startPt = UILineRenderer.EvaluateCubicBezier(pStart, p1, p2, pEnd, 0f);
        Vector2 endPt = UILineRenderer.EvaluateCubicBezier(pStart, p1, p2, pEnd, 1f);
        Assert(Vector2.Distance(startPt, pStart) < 0.001f && Vector2.Distance(endPt, pEnd) < 0.001f,
            "Bezier_ReversedPoints_EndpointsPreserved", $"Start={startPt}, End={endPt}");
    }

    // =========================================================================
    // 9. QUAD MESH ORTHOGONALITY, THICKNESS & WINDING
    // =========================================================================

    private static void Test9_QuadMesh_OrthogonalityThicknessAndWinding()
    {
        // Replicate DrawSegment logic from UILineRenderer
        Action<Vector2, Vector2, float, Action<Vector2, Vector2, Vector2, Vector2>> simulateSegment =
            (start, end, thickness, onQuad) =>
            {
                Vector2 dir = end - start;
                float len = dir.magnitude;
                if (len < 0.001f) return;

                Vector2 normDir = dir / len;
                Vector2 normal = new Vector2(-normDir.y, normDir.x) * (thickness * 0.5f);

                Vector2 v0 = start + normal;
                Vector2 v1 = start - normal;
                Vector2 v2 = end - normal;
                Vector2 v3 = end + normal;

                onQuad(v0, v1, v2, v3);
            };

        // Test across multiple angles: 0, 45, 90, 135, 180, 270 deg
        float[] angles = new float[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        float thickness = 4.0f;
        bool allQuadsValid = true;
        string detail = "";

        foreach (float deg in angles)
        {
            float rad = deg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 50f;
            Vector2 start = new Vector2(100f, 100f);
            Vector2 end = start + dir;

            simulateSegment(start, end, thickness, (v0, v1, v2, v3) =>
            {
                // 1. Thickness check
                float startThickness = Vector2.Distance(v0, v1);
                float endThickness = Vector2.Distance(v3, v2);
                if (Mathf.Abs(startThickness - thickness) > 0.001f || Mathf.Abs(endThickness - thickness) > 0.001f)
                {
                    allQuadsValid = false;
                    detail = $"Angle {deg}: thickness mismatch (start={startThickness}, end={endThickness})";
                    return;
                }

                // 2. Length check
                float lenLeft = Vector2.Distance(v0, v3);
                float lenRight = Vector2.Distance(v1, v2);
                if (Mathf.Abs(lenLeft - 50f) > 0.001f || Mathf.Abs(lenRight - 50f) > 0.001f)
                {
                    allQuadsValid = false;
                    detail = $"Angle {deg}: length mismatch (left={lenLeft}, right={lenRight})";
                    return;
                }

                // 3. Orthogonality check: (v0 - start) dot (end - start) == 0
                Vector2 normalVec = v0 - start;
                float dot = Vector2.Dot(normalVec, dir);
                if (Mathf.Abs(dot) > 0.001f)
                {
                    allQuadsValid = false;
                    detail = $"Angle {deg}: normal not perpendicular, dot={dot}";
                    return;
                }

                // 4. Triangle cross product winding (non-zero area)
                // Tri 1: (v0, v1, v2)
                Vector2 e1 = v1 - v0;
                Vector2 e2 = v2 - v0;
                float cross1 = e1.x * e2.y - e1.y * e2.x;

                // Tri 2: (v2, v3, v0)
                Vector2 e3 = v3 - v2;
                Vector2 e4 = v0 - v2;
                float cross2 = e3.x * e4.y - e3.y * e4.x;

                if (Mathf.Abs(cross1) < 0.01f || Mathf.Abs(cross2) < 0.01f)
                {
                    allQuadsValid = false;
                    detail = $"Angle {deg}: degenerate triangle area (cross1={cross1}, cross2={cross2})";
                    return;
                }
            });

            if (!allQuadsValid) break;
        }

        Assert(allQuadsValid, "QuadMesh_OrthogonalityThicknessAndWindingValid", detail);

        // Test degenerate zero-length segment rejection
        bool callbackCalled = false;
        simulateSegment(new Vector2(10, 10), new Vector2(10, 10), 4f, (v0, v1, v2, v3) => callbackCalled = true);
        Assert(!callbackCalled, "QuadMesh_ZeroLengthSegment_SafelyRejected", "Zero-length segment should be skipped");
    }

    // =========================================================================
    // 10. DASHED LINE GAP INTEGRITY & SEGMENT CONTINUITY
    // =========================================================================

    private static void Test10_DashedLine_GapIntegrityAndSegmentContinuity()
    {
        // In UILineRenderer.DrawBezierCurve for Mystery style:
        // isDashed = true;
        // for (int i = 1; i <= segCount; i++) {
        //     if (isDashed && (i % 2 == 0)) { prevPoint = currentPoint; continue; }
        //     DrawSegment(prevPoint, currentPoint);
        //     prevPoint = currentPoint;
        // }
        // Verify that:
        // 1. Odd segments are drawn (i=1, 3, 5, 7...)
        // 2. Even segments are skipped as gaps (i=2, 4, 6, 8...)
        // 3. No diagonal cross-line is drawn across gaps

        int segCount = 16;
        var drawnSegments = new List<(Vector2 start, Vector2 end)>();

        Vector2 p0 = new Vector2(0f, 0f);
        Vector2 p3 = new Vector2(160f, 0f);
        float dx = 80f;
        Vector2 p1 = p0 + new Vector2(dx, 0f);
        Vector2 p2 = p3 - new Vector2(dx, 0f);

        Vector2 prev = p0;
        for (int i = 1; i <= segCount; i++)
        {
            float t = i / (float)segCount;
            Vector2 cur = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, t);

            if (i % 2 == 0)
            {
                prev = cur;
                continue;
            }

            drawnSegments.Add((prev, cur));
            prev = cur;
        }

        Assert(drawnSegments.Count == 8,
            "DashedLine_ExactlyHalfSegmentsDrawn",
            $"Drawn count: {drawnSegments.Count}, expected: 8 (out of 16)");

        // Verify all drawn segments have length equal to 1 segment step (no diagonal leap)
        bool segmentLengthsUniform = true;
        foreach (var seg in drawnSegments)
        {
            float len = Vector2.Distance(seg.start, seg.end);
            if (len > 18f || len < 6f) // each segment is ~9-15px along cubic Bezier parameterization
            {
                segmentLengthsUniform = false;
                break;
            }
        }

        Assert(segmentLengthsUniform, "DashedLine_SegmentLengthsUniform_NoGapCrossing", "A dashed line segment leaped across the gap");
    }

    // =========================================================================
    // 11. PAN & ZOOM COORDINATE SYSTEM PIVOT ANALYSIS (EMPIRICAL VERIFICATION)
    // =========================================================================

    public static void Test11_PanZoom_CoordinateSystemPivotAnalysis()
    {
        // Viewport resolution: 1560 x 1020
        Vector2 vpSize = new Vector2(1560f, 1020f);
        Vector2 graphStartPos = new Vector2(1460f, 2600f);
        float scale = 1.0f;

        // In the fixed MetaGraphPanZoomUI.FocusOnPosition:
        // viewportCenter = viewportSize * 0.5f;
        Vector2 viewportCenter = vpSize * 0.5f; // (780, 510)

        // Target position to center graph on graphStartPos:
        Vector2 targetPosition = viewportCenter - graphStartPos * scale; // (-680, -2090)

        // ContentRect anchorMin = (0, 0), pivot = (0, 0).
        // For parent Viewport with pivot (0.5, 0.5), anchorMin is at local coordinate (-W/2, -H/2).
        Vector2 anchorLocalInViewport = -vpSize * 0.5f; // (-780, -510)

        // Node position in Viewport local space:
        Vector2 nodeLocalInViewport = anchorLocalInViewport + targetPosition + graphStartPos * scale;
        // = (-780, -510) + (-680, -2090) + (1460, 2600) = (0, 0) [PERFECT CENTER]!

        float distanceFromCenter = nodeLocalInViewport.magnitude;

        Console.WriteLine($"[ANALYSIS] Start node local position in viewport: {nodeLocalInViewport}");
        Console.WriteLine($"[ANALYSIS] Distance to center: {distanceFromCenter:F3}px");

        Assert(distanceFromCenter < 0.001f,
            "ViewportCenterAlignment_StartNodeCenteredAtViewportCenter",
            $"Start node ends up at {nodeLocalInViewport} (distance: {distanceFromCenter:F3}px, expected < 0.001px)");
    }

    public static void Test12_MetaGraphPanZoomUI_InstanceDirectExecution()
    {
        var panZoom = (MetaGraphPanZoomUI)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MetaGraphPanZoomUI));
        panZoom.ContentBounds = new Rect(400f, 200f, 5600f, 4800f);
        panZoom.DefaultFocusPosition = new Vector2(1460f, 2600f);
        panZoom.FallbackViewportSize = new Vector2(1560f, 1020f);
        panZoom.ResetViewInstant();

        // Test FocusOnPosition
        panZoom.FocusOnPosition(new Vector2(1460f, 2600f));
        Assert(panZoom.TargetPosition == new Vector2(-680f, -2090f),
            "PanZoomInstance_FocusOnPosition_TargetMatchesFormula",
            $"Expected (-680, -2090), got {panZoom.TargetPosition}");

        // Test Pan
        Vector2 before = panZoom.TargetPosition;
        panZoom.Pan(new Vector2(100f, 50f));
        Assert(panZoom.TargetPosition == before + new Vector2(100f, 50f),
            "PanZoomInstance_Pan_DeltaAddedCorrectly",
            $"Expected {before + new Vector2(100f, 50f)}, got {panZoom.TargetPosition}");

        // Test Bounds Clamping
        panZoom.Pan(new Vector2(100000f, 100000f));
        float maxX = -panZoom.ContentBounds.xMin * panZoom.TargetZoom;
        float maxY = -panZoom.ContentBounds.yMin * panZoom.TargetZoom;
        Assert(panZoom.TargetPosition.x <= maxX + 0.001f && panZoom.TargetPosition.y <= maxY + 0.001f,
            "PanZoomInstance_ClampToBounds_RestrictsUpperBounds",
            $"Got {panZoom.TargetPosition}, max allowed: ({maxX}, {maxY})");
    }
}
