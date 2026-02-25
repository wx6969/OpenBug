# OpenAnt 项目架构与开发大纲

## 1. 项目愿景
构建一个基于 **WPF (Windows Presentation Foundation)** 的高性能桌面宠物蚂蚁。
核心特色是 **程序化动画 (Procedural Animation)**，而非播放预制帧动画。这意味着蚂蚁能根据移动速度、旋转角度和环境实时计算腿部动作，表现出极高的生物真实感（如急停、倒退、爬过图标）。

## 2. 技术架构大纲

### 核心层 (Core)
- **Overlay Window**: 全屏透明、穿透点击的 WPF 窗口（已实现）。
- **Game Loop**: 基于 `CompositionTarget.Rendering` 的高频渲染循环。
- **Vector Math**: 使用 C# `System.Windows.Vector` 进行向量计算（移植自 Unity `Vector3` 逻辑）。

### 运动学模块 (Kinematics Module)
参考 *jerejoensuu* 和 *PhilS94* 的 Unity 项目，移植核心算法到 WPF：

1.  **逆向运动学 (IK Solver)**
    *   **当前实现**: 2-Bone Analytic Solver (两段式解析解)。利用余弦定理计算膝盖角度。
    *   **进阶目标**: 
        *   引入 **FABRIK (Forward And Backward Reaching Inverse Kinematics)** 算法（参考 *Unity-Procedural-IK*），支持多关节触角或更复杂的腿部结构。
        *   **Ground Alignment (地面贴合)**: 根据“地面”（屏幕边缘或窗口边缘）的法线旋转身体。

2.  **步态控制器 (Gait Controller)**
    *   **当前实现**: 动态三角步态 (Dynamic Tripod Gait)。将 6 条腿分为 A/B 两组，根据误差阈值 (`StepTriggerThreshold`) 交替迈步。
    *   **优化方向**: 
        *   **速度预测 (Velocity Prediction)**: 目前已加入，根据当前速度预判落点。
        *   **身体惯性**: 身体位置应略微滞后于腿部的支撑中心，模拟生物的“悬挂感”。

### 行为智能 (AI & Behavior)
参考 *metapika* 的昆虫行为逻辑：

1.  **漫游 (Wandering)**: 基于 Perlin Noise 或随机向量的平滑移动。
2.  **交互 (Interaction)**:
    *   **鼠标交互**: 逃离（Flee）或追踪（Seek）鼠标。
    *   **窗口交互**: 识别桌面窗口句柄（User32.dll），让蚂蚁在窗口标题栏上行走（视为“墙壁”）。
3.  **状态机 (FSM)**: Idle（发呆清洁触角）、Explore（探索）、Panic（惊慌逃窜）。

## 3. 开发路线图 (结合参考资源)

### 第一阶段：基础运动与骨骼 (已完成 90%)
*   [x] 建立 2D 骨骼模型 (Head, Thorax, Abdomen)。
*   [x] 实现 2 段式 IK 算法 (Legs)。
*   [x] 实现基础三角步态 (Tripod Gait)。
*   [x] **关键优化**: 解决“滑步”、“瞬移”、“隐形腿”问题（通过伪透视和插值优化）。

### 第二阶段：物理感与细节 (进行中)
*   **参考资源**: *PhilS94 / Unity-Procedural-IK-Wall-Walking-Spider*
*   **任务**:
    1.  **身体姿态控制**: 当腿部迈向不同高度或角度时，身体应自动倾斜（Body Tilt）。在 2D 中可以通过改变各身体段的相对位置来模拟“伪 3D”倾斜。
    2.  **触角物理**: 为触角添加 `Verlet Integration` (韦尔莱积分) 或简易弹簧物理，使其在移动时自然甩动。
    3.  **平滑转向**: 蚂蚁转向时，头部先转，身体跟随，最后是尾部（Snake-like movement）。

### 第三阶段：桌面环境交互 (高级)
*   **参考资源**: *Godot 2D Chain Simulation* / *Unity Raycast Logic*
*   **任务**:
    1.  **屏幕边缘检测**: 当蚂蚁走到屏幕边缘时，不再是简单的反弹，而是旋转身体，“附着”在边缘爬行。
    2.  **窗口攀爬**: 使用 Windows API 获取窗口矩形 (Rect)，将窗口边缘视为可行走的路径。
    3.  **多蚂蚁群体**: 引入简单的群体行为（Boids 算法），让多只蚂蚁排队或聚集。

## 4. 关键算法移植指南 (Unity -> WPF)

| Unity 概念 | WPF/C# 对应实现 | 备注 |
| :--- | :--- | :--- |
| `Vector3` | `System.Windows.Vector` / `Point` | WPF 是 2D 坐标系，Z 轴通常用 Y 轴透视或缩放模拟。 |
| `Update()` | `CompositionTarget.Rendering` | 均基于帧驱动。注意 WPF 的 `DeltaTime` 需要手动计算。 |
| `Transform.LookAt()` | `Math.Atan2(y, x)` | 2D 旋转计算核心。 |
| `Mathf.Lerp()` | `d + (t - d) * k` | 自定义 `MathUtils.Lerp`，逻辑完全一致。 |
| `Raycast` | 几何计算 (点线距离) | 2D 环境下不需要物理引擎，直接计算点与矩形边缘的距离即可。 |

## 5. 当前代码结构优化建议
*   **配置分离**: 保持 `AntConfig` 的独立性，便于实时调整参数（如步幅、速度）。
*   **渲染分离**: 目前逻辑与 UI 耦合在 `ProceduralAnt` 中。未来可考虑将“数据计算”与“UI 更新”分离，以便在后台线程运行高开销的群体 AI 计算。


## a. 参考项目
1. Unity 引擎生态（C# 语言，极易移植到您的 WPF 项目）

因为 Unity 也是用 C# 开发的，所以这些项目的源码（特别是计算向量、角度和距离的数学库代码）对您的参考价值最大。

    PhilS94 / Unity-Procedural-IK-Wall-Walking-Spider

        链接：https://github.com/PhilS94/Unity-Procedural-IK-Wall-Walking-Spider

        亮点：极其成熟的项目，实现了一只会沿着墙壁和天花板爬行的蜘蛛。它的步态算法非常完善，如果您想让您的桌面蚂蚁沿着各种软件窗口的边缘爬行，这个项目的法线检测和重力吸附代码是完美的教科书。

    jerejoensuu / procedural-animation

        链接：https://github.com/jerejoensuu/procedural-animation

        亮点：专注于蜘蛛的程序化 IK。代码结构清晰，使用了 FastIK 求解器，包含自由度计算，适合研究其腿部的落点判定和移动时机（如何决定哪条腿该迈步）。

    metapika / unity-procedural-animation

        链接：https://github.com/metapika/unity-procedural-animation

        亮点：明确指出是为虫子（Bugs）和蜘蛛类生物制作的程序化运动系统。如果您需要昆虫专属的动作感，这个库的逻辑比普通的人形 IK 更对口。

    timi-ty / procedural-animation

        链接：https://github.com/timi-ty/procedural-animation

        亮点：直接针对“昆虫（Insect）”的程序化动画，利用 IK 和蒙皮网格驱动运动。

2. Godot 引擎生态（轻量级、包含大量 2D 实现）

Godot 的 2D 骨骼和链条模拟技术非常适合桌面 2D 宠物开发。

    AmanVerma0047 / Procedural-Animated-Creatures-in-Godot

        链接：https://github.com/AmanVerma0047/Procedural-Animated-Creatures-in-Godot

        亮点：这是一个专门针对 2D 链条模拟（2D Chain Simulation） 的教程库，非常贴合您 2D 俯视视角的需求。里面包含了各种动物的 2D 程序化动画代码，可以直接提取其 2D 坐标系的运动约束公式。

3. Unreal Engine 生态（C++）

如果您未来打算用 C++ 和 Direct2D 走极致性能的底层渲染路线，可以参考虚幻引擎的实现。

    pz64 / procedural-animated-spider

        链接：https://github.com/pz64/procedural-animated-spider

        亮点：在 UE4 中实现的程序化蜘蛛，逻辑更加贴近底层的数学向量计算。