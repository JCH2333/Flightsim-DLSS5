using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 用户协议与免责声明文本。协议修订号变化会要求所有用户重新阅读确认。
/// 起草依据：《民法典》第469/490条（数据电文形式的书面合同与成立）、第496条（格式条款的
/// 提示说明义务——故采用显著弹窗+完整展示+滚动阅读+主动点击的确认方式，并本地留存修订号
/// 与同意时间作为已尽提示义务的证据，见《民法典合同编通则解释》第10条的举证要求）、
/// 第497/506条（免责条款的无效边界——故声明不排除法定不得免除的责任）；《著作权法》第24条
/// 与《计算机软件保护条例》第17条（为个人学习、研究使用他人已发表作品/软件的合理使用定位）。
/// </summary>
public static class AgreementContent
{
    /// <summary>协议修订号：修改正文后必须更新，未重新同意前不得使用软件。</summary>
    public const string Revision = "2026-09-14-v1";

    public const string AuthorName = "B站 一只剑齿虎呀";
    public const string AuthorUrl = "https://space.bilibili.com/472309803";

    public static readonly (string Title, string Body)[] Documents =
    {
        (L.S("用户使用协议", "User Agreement"), L.S(UserAgreementZh, UserAgreementEn)),
        (L.S("免责声明", "Disclaimer"), L.S(DisclaimerZh, DisclaimerEn)),
    };

    private const string UserAgreementZh = """
        生效日期：2026年9月14日　　协议修订号：2026-09-14-v1

        请在安装、使用本软件前完整阅读本《用户使用协议》与《免责声明》。滚动阅读完毕并点击「同意并继续使用」，即表示您已阅读、理解并接受全部内容；如不同意，请点击「不同意并退出」，本软件将关闭且您不应继续使用。

        一、软件性质
        1. DLSS5Patcher（下称“本软件”）是由网络作者“一只剑齿虎呀”维护的免费本地工具，用于辅助将第三方神经渲染组件（OptiScaler、NVIDIA DLSS 运行时、DLSS5-Feeder 等）安装、配置到您已合法拥有的模拟飞行软件中。
        2. 本软件完全免费，不出售、不捆绑收费、不提供付费授权；任何向他人收费出售、代装本软件的行为均未获作者授权。
        3. 本软件不要求注册账号，不上传行为数据；您主动提交的问题反馈为匿名内容，仅用于定位与修复问题。

        二、同意的方式与效力
        1. 本协议以电子数据电文形式展示并订立。软件以显著弹窗、完整展示全文、阅读进度确认与主动点击同意的方式征求您的确认，并记录协议修订号与同意时间。依据《民法典》第469条、第490条及《电子签名法》，您点击同意后本协议即对双方具有约束力。
        2. 协议修订号变更后，软件会要求所有用户重新阅读并确认；未重新同意前不得继续使用。

        三、使用前提与用户承诺
        1. 您应自行合法取得并拥有 Microsoft Flight Simulator、X-Plane 12 等目标软件的正本授权。依据《著作权法》第24条与《计算机软件保护条例》第17条，本软件及组件仅限您为个人学习、研究为目的使用。
        2. 严禁将本软件或其安装的组件用于任何商业用途，包括但不限于：收费出售、捆绑销售、代装收费、打包分发牟利；严禁用于盗版分发或规避技术保护措施。
        3. 自愿赞助仅用于支持免费维护，不构成任何软件、授权或服务的对价。

        四、安装行为与本地文件
        1. 安装过程会在您指定的游戏目录内新增、替换文件（如 OptiScaler.ini、nvngx_dlssnr.dll 等），并可能需要管理员权限。安装前请您自行完整备份重要文件；软件提供的卸载/还原能力不构成对全部情形的保证。
        2. 首次安装会从作者自建服务器或 GitHub 下载组件包（经 SHA-256 完整性校验）并缓存至本地，之后可离线重装。

        五、法律适用
        1. 本协议依据中华人民共和国法律订立。如个别条款被认定无效，不影响其余条款的效力；本协议不排除、不限制法律强制性规定所赋予您的权利。
        """;

    private const string UserAgreementEn = """
        Effective date: 2026-09-14. Revision: 2026-09-14-v1.

        This tool (DLSS5Patcher) is a free, unofficial local helper that installs and configures third-party neural-rendering components (OptiScaler, NVIDIA DLSS runtimes, DLSS5-Feeder) for flight simulators you legally own. The Chinese text of this agreement governs. Summary in English:
        1. Personal study and research use ONLY. Strictly no commercial use, resale, bundled distribution or piracy.
        2. You must own a legal copy of the target simulator. You are responsible for verifying what you may install.
        3. The installer adds/replaces files in the game folder you choose; back up your data first. Packages are downloaded from the author's server or GitHub and verified by SHA-256.
        4. Voluntary donations support free maintenance and are not a payment for any product or service.
        5. By clicking "Agree" you accept the full Chinese text of the User Agreement and Disclaimer.
        """;

    private const string DisclaimerZh = """
        请特别阅读本免责声明。它旨在以显著方式说明软件性质、风险边界与责任分配，不构成对法定责任的排除（《民法典》第506条规定的无效情形除外，见第四条）。

        一、非官方与权利归属
        1. 本软件及所安装的组件均为非官方社区内容，不隶属于 Microsoft、Asobo、Xbox、Laminar Research、NVIDIA、AMD 及 OptiScaler/ReShade 等项目的开发者，亦不代表其立场或获得其授权。
        2. Microsoft Flight Simulator、X-Plane、DLSS、OptiScaler 等名称、商标与程序的权利归各自权利人所有。本软件仅是便于学习研究的本地配置辅助，不应被理解为官方修改或兼容性承诺。

        二、按“现状”提供
        1. 本软件及组件按“现状”“现有”提供。作者不保证其无错误、不中断、适配所有游戏版本、硬件、驱动或安全软件。
        2. 游戏更新后组件可能失效、冲突或需要重装；下载源可能因网络、限流、下架或不可抗力而不可用。这些均不构成作者违反义务。

        三、风险提示与责任边界
        1. 替换游戏目录文件、注册渲染层、运行组件均存在固有风险。请您在操作前自行备份；对于未备份、误操作、目录选错或擅自改动造成的损失，在法律允许的范围内作者不承担责任。
        2. 依据《民法典》第506条，造成对方人身损害的、或因故意或重大过失造成对方财产损失的免责条款无效——本声明不排除上述法定不得免除的责任，也不限制您依法享有的任何权利。

        四、版权与投诉
        1. 若您是权利人并认为本软件或其分发的内容侵害了您的权益，请通过粉丝群或 B 站私信联系作者，核实后将及时停止分发并删除相关内容。
        2. 如您不能接受本声明的全部内容，请点击「不同意并退出」，并停止使用本软件。
        """;

    private const string DisclaimerEn = """
        The software and installed components are unofficial community content, provided "as is" without warranty of any kind. All trademarks and third-party programs (Microsoft Flight Simulator, X-Plane, DLSS, OptiScaler, ReShade and others) belong to their respective owners; this tool is not affiliated with or endorsed by them. Use is limited to personal study and research; no commercial use is permitted. To the maximum extent permitted by law the author is not liable for losses caused by using this tool, except where liability cannot be excluded by law (Civil Code art. 506). Rights holders may request removal via the fan group or Bilibili private message. The Chinese text of the agreements governs.
        """;
}
