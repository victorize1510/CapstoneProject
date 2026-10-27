using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Owns only scroll presentation; existing filter buttons keep their callbacks and values.</summary>
    public sealed class CompanionFilterRail : VisualElement {
        readonly ScrollView viewport;
        readonly AngularButton previous, next;
        readonly RailFade leftFade = new RailFade(false), rightFade = new RailFade(true);
        public CompanionFilterRail(VisualElement filters) {
            name = "companion-filter-rail"; pickingMode = PickingMode.Ignore;
            var face = new AngularSurface { Shape = "panel", Role = "panel", NoBorder = false };
            Stretch(face); Add(face);
            viewport = new ScrollView(ScrollViewMode.Horizontal) { name = "green-filter-viewport" };
            viewport.horizontalScrollerVisibility = ScrollerVisibility.Hidden; viewport.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            Place(viewport, 34, 4, 400, 36); Add(viewport); viewport.Add(filters);
            filters.style.position = Position.Relative; filters.style.left = filters.style.top = 0; filters.style.width = StyleKeyword.Auto; filters.style.height = 36; filters.style.overflow = Overflow.Visible;
            viewport.contentViewport.style.overflow = Overflow.Hidden;
            previous = new AngularButton(() => Shift(-160)) { name = "skills-filter-previous", text = "‹", tooltip = "Các hệ phía trước", Role = "raised", Shape = "parallel" };
            next = new AngularButton(() => Shift(160)) { name = "skills-filter-next", text = "›", tooltip = "Các hệ phía sau", Role = "raised", Shape = "parallel" };
            foreach (var b in new[] { previous, next }) { b.SetAngularContext(true); b.AddToClassList("filter-rail-arrow"); b.style.fontSize = 24; }
            Place(previous, 2, 4, 30, 36); Place(next, 436, 4, 30, 36); Add(previous); Add(next);
            Place(leftFade, 34, 4, 16, 36); Place(rightFade, 418, 4, 16, 36); Add(leftFade); Add(rightFade);
            var all=filters.Q<Button>("skills-filter-all");
            foreach (var button in filters.Query<Button>().ToList()) { var captured = button; button.clicked += () => viewport.ScrollTo(all!=null&&all.ClassListContains("is-selected")?all:captured); }
            viewport.horizontalScroller.valueChanged += _ => UpdateEdges();
            viewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateEdges()); filters.RegisterCallback<GeometryChangedEvent>(_ => UpdateEdges());
            RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(UpdateEdges));
        }
        void Shift(float amount) { viewport.scrollOffset = new Vector2(Mathf.Clamp(viewport.scrollOffset.x + amount, 0, viewport.horizontalScroller.highValue), 0); UpdateEdges(); }
        void UpdateEdges() {
            bool overflow = viewport.horizontalScroller.highValue > 1;
            previous.style.display = next.style.display = overflow ? DisplayStyle.Flex : DisplayStyle.None;
            bool canLeft = overflow && viewport.scrollOffset.x > 1, canRight = overflow && viewport.scrollOffset.x < viewport.horizontalScroller.highValue - 1;
            previous.SetEnabled(canLeft); next.SetEnabled(canRight);
            leftFade.style.display = canLeft ? DisplayStyle.Flex : DisplayStyle.None; rightFade.style.display = canRight ? DisplayStyle.Flex : DisplayStyle.None;
        }
        static void Place(VisualElement e, float x, float y, float width, float height) { e.style.position = Position.Absolute; e.style.left = x; e.style.top = y; e.style.width = width; e.style.height = height; }
        static void Stretch(VisualElement e) { e.style.position = Position.Absolute; e.style.left = e.style.top = e.style.right = e.style.bottom = 0; }
        sealed class RailFade : VisualElement {
            static readonly ushort[] Indices={0,1,2,0,2,3};
            readonly bool reverse;
            public RailFade(bool right) { reverse = right; pickingMode = PickingMode.Ignore; generateVisualContent += Paint; }
            void Paint(MeshGenerationContext ctx) {
                var mesh = ctx.Allocate(4, 6); Color a = GreenAngularGeometry.Panel, b = a; a.a = reverse ? 0 : 1; b.a = reverse ? 1 : 0;
                mesh.SetNextVertex(new Vertex { position = new Vector3(0, 0, Vertex.nearZ), tint = a }); mesh.SetNextVertex(new Vertex { position = new Vector3(layout.width, 0, Vertex.nearZ), tint = b });
                mesh.SetNextVertex(new Vertex { position = new Vector3(layout.width, layout.height, Vertex.nearZ), tint = b }); mesh.SetNextVertex(new Vertex { position = new Vector3(0, layout.height, Vertex.nearZ), tint = a });
                foreach (ushort i in Indices) mesh.SetNextIndex(i);
            }
        }
    }
}
