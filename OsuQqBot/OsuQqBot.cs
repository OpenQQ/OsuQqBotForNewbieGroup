﻿using OsuQqBot.Api;
using OsuQqBot.Functions;
using OsuQqBot.QqBot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsuQqBot
{
    /// <summary>
    /// 只能用于CQ平台
    /// </summary>
    public partial class OsuQqBot
    {
        IQqBot qq;
        readonly LocalData.Database database = new LocalData.Database(Paths.DataPath);

        public OsuQqBot(IQqBot qqBot)
        {
            qq = qqBot;
            CurrentQq = qq.GetLoginQq();
            Config config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(Paths.JsonConfigPath));
            id_Kamisama = config.Kamisama;
            GroupId = config.MainGroup;
            foreach (var item in config.ValidGroups)
            {
                ValidGroups.Add(item);
            }

            try
            {
                var ignoreLines = File.ReadAllLines(Paths.IgnoreListPath);
                if (ignoreLines.Length == 2)
                {
                    ignoreList = Newtonsoft.Json.JsonConvert.DeserializeObject<HashSet<long>>(ignoreLines[0]);
                    ignorePPList = Newtonsoft.Json.JsonConvert.DeserializeObject<HashSet<long>>(ignoreLines[1]);
                }
            }
            catch (FileNotFoundException)
            { }
            if (ignoreList == null) ignoreList = new HashSet<long>();
            if (ignorePPList == null) ignorePPList = new HashSet<long>();

            apiClient = new OsuApiClient(config.ApiKey);
        }

        /// <summary>
        /// 被At时升级用户数据
        /// </summary>
        /// <param name="context"></param>
        public async Task<bool> UpdateUserBandingAsync(long group, long qq, string message)
        {
            if (group == GroupId && !ignoreList.Contains(qq))
                if (message.Contains($"[CQ:at,qq={CurrentQq}]"))
                {
                    var uid = await FindUid(qq);
                    if (!uid.HasValue)
                    {
                        this.qq.SendGroupMessageAsync(group, "网络错误，请再试一次");
                        return false;
                    }
                    else if (uid.Value == 0)
                    {
                        this.qq.SendGroupMessageAsync(group, "爷爷并没有绑定，无法更新");
                        return false;
                    }
                    var username = await FindUsername(uid.Value, true);
                    if (username == null)
                    {
                        this.qq.SendGroupMessageAsync(group, "网络错误，请再试一次");
                        return true;
                    }
                    if (string.IsNullOrEmpty(username)) this.qq.SendGroupMessageAsync(group, "咦？爷爷被办了？");
                    else this.qq.SendGroupMessageAsync(group, $"用户名更新为{username}");

                    //this.qq.SendPrivateMessageAsync(id_Kamisama, group.ToString() + " " + qq.ToString());
                    return true;
                }
            return false;
        }

        // 暂不支持
        private void Query(EndPoint endPoint, string username) => throw new NotImplementedException();
        private async void Query(EndPoint endPoint, long uid, string para = "")
        {
            switch (endPoint)
            {
                case GroupEndPoint g:
                    Query(g.GroupId, uid, para);
                    break;
                default:
                    //string message;
                    (bool success, string message) = await ProcessQuery(uid, para);
                    qq.SendMessageAsync(endPoint, message, true);
                    break;
            }
        }

        /// <summary>
        /// （执行）查询玩家信息，返回字符串
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="m">模式，可选0，1，2，3</param>
        /// <returns></returns>
        private async Task<(bool success, string info)> ProcessQuery(long uid, string para = "")
        {
            bool success;
            string message;
            var mode = OsuApiClient.Mode.Unspecified;
            var paras = para.Split();
            if (paras.Any(p => p == "0" || p.ToLowerInvariant() == "std")) mode = OsuApiClient.Mode.Std;
            else if (paras.Any(p => p == "1" || p.ToLowerInvariant() == "taiko")) mode = OsuApiClient.Mode.Taiko;
            else if (paras.Any(p => p == "2" || p.ToLowerInvariant() == "ctb" || p.ToLowerInvariant() == "catch")) mode = OsuApiClient.Mode.Ctb;
            else if (paras.Any(p => p == "3" || p.ToLowerInvariant() == "mania")) mode = OsuApiClient.Mode.Mania;

            var users = await apiClient.GetUserAsync(uid.ToString(), OsuApiClient.UsernameType.User_id, mode);
            if (users == null) { success = false; message = "网络错误"; }
            else if (!users.Any()) { success = false; message = "没这个人！"; }
            else
            {
                User user = users[0];

                StringBuilder sb = new StringBuilder();
                sb.Append(users[0].username + "的个人信息")
                    .Append(mode == OsuApiClient.Mode.Unspecified ? "" : "—" + mode.GetModeString()).AppendLine();
                sb.AppendLine();
                sb.AppendLine(users[0].pp_raw + "pp 表现");
                sb.Append("#" + users[0].pp_rank)
                    .AppendLine(" (" + user.Country + " #" + user.CountryRank + ")");
                string displayAcc;
                try
                {
                    displayAcc = users[0].accuracy.Substring(0, 5);
                }
                catch (ArgumentOutOfRangeException)
                {
                    displayAcc = users[0].accuracy;
                }
                sb.AppendLine(users[0].ranked_score + " Ranked谱面总分");
                sb.AppendLine(displayAcc + "% 准确率");
                sb.AppendLine(users[0].playcount + " 游玩次数");
                sb.Append(((User)users[0]).Tth + " 总命中次数");
                message = sb.ToString();
                success = true;
            }
            return (success, message);
        }

        /// <summary>
        /// QQ群专用查询。给Query传入GroupEndPoint会调用此方法
        /// </summary>
        /// <param name="group"></param>
        /// <param name="uid"></param>
        /// <param name="para"></param>
        private async void Query(long group, long uid, string para = "")
        {
            var memberList = qq.GetGroupMemberList(group);
            //if (memberList.Any(m => m.Qq == 2478057279))
            //{// inter
            //    this.qq.SendGroupMessageAsync(group, $"!stats {uid}" +
            //    (
            //        string.IsNullOrEmpty(para) ? string.Empty :
            //            (char.IsLetterOrDigit(para[0]) ? "," : string.Empty)
            //            + $"{para}"
            //    ), true);
            //}
            //else 
            if (memberList.Any(m => m.Qq == 1335734629))
            {// 白菜
                if (para.Length > 0 && !para.StartsWith('#')) para = "#" + para;
                this.qq.SendGroupMessageAsync(group, $"!statu {uid} {para}", true);
            }
            else
            {
                (bool success, string message) = await ProcessQuery(uid, para);
                if (success)
                {
                    string tip = GetTip(group);
                    if (!string.IsNullOrWhiteSpace(tip))
                        message += Environment.NewLine + Environment.NewLine + tip;
                }
                this.qq.SendGroupMessageAsync(group, message);
            }
        }

        private string GetTip(long group) =>
            group == 514661057 || group == 614892339 ?
            this.Tips[this.random.Next(this.Tips.Count)] :
            string.Empty;

        Random random = new Random();
        private readonly List<string> Tips = new List<string>
        {
            "你不合适屙屎——interBot",
            "求求你别糊图了——interBot",
            "再见了——interBot",
            "pp刷子——interBot",
            "打图经验充足，不飞升没理由——interBot",
            "再@我，我叫咩咩打你——我说的",
            "whir爷爷——大家如是说",
            "标准的正常玩家——interBot",
            "你们还是去床上解决吧——interBot",
            "相信你一定可以克服瓶颈",
            At(1677323371)+"你快回来，生命因你而精彩",
            At(1677323371)+"你快回来，把我的思念带回来",
            "别让我的心空如大海——《你快回来》",
            "新人赛火热进行中（化学式承办）",
            "广告位招租",
            "中国的whir，我被他打爆！",
        };

        /// <summary>
        /// 根据某人的昵称，或者At某人快速查询数据
        /// </summary>
        /// <param name="group"></param>
        /// <param name="qq"></param>
        /// <param name="message"></param>
        /// <returns>是否已处理（无论是否查询成功）</returns>
        public bool WhirIsBest(long group, long qq, string message)
        {
            string queryAtPatten = @"^\s*查\s*\[CQ:at,qq=(\d+)\]\s*(.*)$";
            var queryAtRegex = new System.Text.RegularExpressions.Regex(queryAtPatten);
            var atMatch = queryAtRegex.Match(message);
            if (atMatch.Success)
            {
                if (group == GroupId)
                {
                    // 判断一下群名片
                }
                Task.Run(async () =>
                {
                    long? aimUid = await FindUid(long.Parse(atMatch.Groups[1].Value));
                    if (aimUid.HasValue)
                        if (aimUid.Value != 0)
                            Query(group, aimUid.Value, atMatch.Groups[2].Value);
                        else this.qq.SendGroupMessageAsync(group, "此人未绑定 id");
                });
                return true;
            }
            if (message.HasCQFunction()) return false;
            message = message.Replace("&#91;", "[").Replace("&#93;", "]").Replace("&amp;", "&");

            string queryPatten = @"^\s*查\s*(\S+)\s*$";
            System.Text.RegularExpressions.Regex queryRegex = new System.Text.RegularExpressions.Regex(queryPatten);
            var queryMatch = queryRegex.Match(message);
            if (queryMatch.Success)
            {
                string wantedNickname = queryMatch.Groups[1].Value.ToLowerInvariant();
                long? uid = database.GetUidFromNickname(wantedNickname);
                if (!uid.HasValue) this.qq.SendGroupMessageAsync(group, $"咱还不知道{queryMatch.Groups[1].Value}是谁呢。", true);
                else Query(group, uid.Value);
                return true;
            }

            string nickPatten = @"^\s*(.+?)\s*叫\s*(\S+)\s*$";
            var nickRegex = new System.Text.RegularExpressions.Regex(nickPatten);
            var nickMatch = nickRegex.Match(message);
            if (nickMatch.Success)
            {
                Task.Run(async () =>
                {
                    string matchUsername = nickMatch.Groups[1].Value;

                    // 检查是否是合法的用户名（减少误触发）
                    var uMatch = regexMatchingUsername.Match(matchUsername);
                    if (!uMatch.Success || uMatch.Groups[1].Value != matchUsername) return;

                    string newNick = nickMatch.Groups[2].Value;
                    if (newNick.EndsWith("，记住了。"))
                    {
                        this.qq.SendGroupMessageAsync(group, "调戏咱很好玩吗？");
                        return;
                    }

                    var users = await apiClient.GetUserAsync(matchUsername, OsuApiClient.UsernameType.Username);
                    if (users == null)
                    {
                        this.qq.SendGroupMessageAsync(group, "再试一次吧~");
                        return;
                    }
                    if (!users.Any())
                    {
                        this.qq.SendGroupMessageAsync(group, "没这个人！叫什么叫！");
                        return;
                    }
                    if (!long.TryParse(users[0].user_id, out long uid))
                    {
                        this.qq.SendGroupMessageAsync(group, "不知道为什么出错了。");
                        return;
                    }
                    var previous = database.SaveNickname(newNick.ToLower(System.Globalization.CultureInfo.InvariantCulture), uid);
                    if (!previous.HasValue)
                    {
                        this.qq.SendGroupMessageAsync(group, $"{users[0].username}叫{newNick}，记住了。", true);
                    }
                    else
                    {
                        string previousUsername = await FindUsername(previous.Value);
                        if (string.IsNullOrEmpty(previousUsername))
                            this.qq.SendGroupMessageAsync(group, $"有时会突然忘了，{newNick}是谁，但是从现在起，{newNick}就是{users[0].username}，记住了！", true);
                        else if (
                            previousUsername.ToLowerInvariant() == users[0].username.ToLowerInvariant()
                        ) this.qq.SendGroupMessageAsync(group, $"咱早就知道{newNick}是{previousUsername}了！", true);
                        else this.qq.SendGroupMessageAsync(group, $"咱还以为{newNick}是{previousUsername}，原来是{users[0].username}，记住了！", true);
                    }
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// 上次检查群名片的时间
        /// </summary>
        private Dictionary<long, DateTime> lastCheckTime = new Dictionary<long, DateTime>();

        /// <summary>
        /// 自动绑定，并检测群名片是否包含ID，并且检测PP是否超限
        /// </summary>
        /// <param name="context"></param>
        public async Task TestInGroupNameAsync(long fromGroup, long fromQq, string message)
        {
            if (!checkSwitch) return;
            if (fromGroup != GroupId) return;
            if (DateTime.UtcNow -
                lastCheckTime.GetValueOrDefault(fromQq, new DateTime(2018, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc))
                <= new TimeSpan(8, 0, 0)) return;

            try
            {
                if (ignoreList.Contains(fromQq)) return;

                long? uid = await FindUid(fromQq);
                if (!uid.HasValue) return;

                if (uid.Value == 0)
                {// 未绑定，开始自动绑定
                    (bool? success, string username, long _uid) = await AutoBind(fromGroup, fromQq);
                    if (!success.HasValue) return;
                    if (success.Value)
                    {
                        this.qq.SendGroupMessageAsync(fromGroup, $"{At(fromQq)}" + Environment.NewLine +
                            $"{username}，你好！" + Environment.NewLine +
                            $"欢迎来到osu!新人群。请发送“~”查询信息。如果你不是{username}，请联系bleatingsheep。"
                        );
                        (bool queryOK, string query) = await ProcessQuery(_uid);
                        if (queryOK) query += Environment.NewLine + Environment.NewLine + "此号数据优秀，同分段中的王者！——interBot";
                        this.qq.SendGroupMessageAsync(fromGroup, query, true);
                    }
                    else
                    {
                        this.qq.SendGroupMessageAsync(fromGroup, $"{At(fromQq)} 你好，请将群名片改为osu!中的名字，以便互相认识。");
                    }
                }
                else
                {// 已绑定，开始检查
                    if (!lastCheckTime.TryAdd(fromQq, DateTime.UtcNow)) lastCheckTime[fromQq] = DateTime.UtcNow;

                    string inGroupName;
                    inGroupName = GetInGroupName(fromGroup, fromQq);
                    if (inGroupName == null) return;

                    var result = await CheckInGroupName(inGroupName, uid.Value);

                    string hint = string.Empty;
                    string hisUsername = await FindUsername(uid.Value);
                    switch (result)
                    {
                        case InGroupNameCheckResult.NeverBind:
                            break;
                        case InGroupNameCheckResult.Error:
                            break;
                        case InGroupNameCheckResult.NotContains:
                            hint = $"为了方便其他人认出您，请修改群名片，必须包括osu!用户名。";
                            break;
                        case InGroupNameCheckResult.NotOwner:
                            hint = $"为了方便其他人认出您，请修改群名片，必须包括正确的osu!用户名。";
                            break;
                        case InGroupNameCheckResult.IsSubstring:
                            hint = "建议修改群名片，不要在用户名前后添加可以被用做用户名的字符，以免混淆。";
                            hint += Environment.NewLine + "建议群名片：" + RecommendCard(inGroupName, hisUsername);
                            break;
                        case InGroupNameCheckResult.Qualified:
                            break;
                        default:
                            break;
                    }
                    if (!string.IsNullOrEmpty(hint))
                    {
                        this.qq.SendGroupMessageAsync(fromGroup, At(fromQq) + Environment.NewLine +
                            hisUsername + "，您好。" + hint);
                    }

                    if (!ignorePPList.Contains(fromQq))
                    {
                        await CheckIfPPOverLimit(fromGroup, fromQq, uid.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                Logger.Log("throw");
                throw;
            }
        }

        /// <summary>
        /// 根据群名片和用户名
        /// </summary>
        /// <param name="card"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        private static string RecommendCard(string card, string username)
        {
            int firstIndex = card.IndexOf(username, StringComparison.InvariantCultureIgnoreCase);
            if (firstIndex != -1)
            {
                string recommendCard = card.Substring(0, firstIndex);
                if (firstIndex != 0)
                    recommendCard += "|";
                recommendCard += username;
                if (firstIndex + username.Length < card.Length)
                {
                    recommendCard += "|" + card.Substring(firstIndex + username.Length);
                }
                return recommendCard;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("无法给出推荐名片");
                sb.AppendLine("群名：" + card);
                sb.AppendLine("用户名：" + username);
                Logger.Log(sb.ToString());
                return null;
            }
        }

        /// <summary>
        /// 不检查群名片和PP的列表
        /// </summary>
        private readonly HashSet<long> ignoreList = null;

        /// <summary>
        /// 不检查PP的列表
        /// </summary>
        private readonly HashSet<long> ignorePPList = null;

        private void SaveIgnoreList()
        {
            var ignoreArray = ignoreList.ToArray();
            var ignoreString = Newtonsoft.Json.JsonConvert.SerializeObject(ignoreArray, Newtonsoft.Json.Formatting.None);
            var ignorePPArray = ignorePPList.ToArray();
            var ignorePPString = Newtonsoft.Json.JsonConvert.SerializeObject(ignorePPArray, Newtonsoft.Json.Formatting.None);
            File.WriteAllLines(Paths.IgnoreListPath, new string[]{
                ignoreString,
                ignorePPString
            });
        }

        /// <summary>
        /// （废弃)（执行）检查群名片
        /// </summary>
        /// <param name="fromGroup"></param>
        /// <param name="fromQq"></param>
        /// <param name="message"></param>
        /// <param name="inGroupName"></param>
        /// <param name="uid"></param>
        /// <returns></returns>
        private async Task CheckIfInGroupNameQualified(long fromGroup, long fromQq, long uid)
        {
            string inGroupName;
            try
            {
                var memberInfo = qq.GetGroupMemberInfo(fromGroup, fromQq);
                if (memberInfo == null)
                {
                    return;
                }
                inGroupName = memberInfo.InGroupName;
                if (string.IsNullOrEmpty(inGroupName))
                    inGroupName = memberInfo.QqNickname;
            }
            catch (FormatException e)
            {// 此 try-catch 好像是遗留问题，可以考虑去掉
                Logger.Log("获取群名片出现问题");
                Logger.Log(fromGroup.ToString());
                Logger.Log(fromQq.ToString());
                Logger.LogException(e);
                Logger.Log("return");
                return;
            }
            var possibleUsernames = ParseUsername(inGroupName);
            if (possibleUsernames.Length == 0)
            {
                await Task.Delay(60000);
                qq.SendGroupMessageAsync(fromGroup, $"[CQ:at,qq={fromQq}] 请修改群名片，必须包括osu!用户名");
                return;
            }

            if (uid != 0)
            {
                string foundUsername = await FindUsername(uid); //找到的用户名
                if (foundUsername == null) return; //查找失败
                if (possibleUsernames.Any(psb =>
                    psb.ToLowerInvariant() == foundUsername.ToLowerInvariant()
                )) return; //OK
                Logger.Log(inGroupName + "用户名不OK" + foundUsername);
                await Task.Delay(60000);
                if (inGroupName.Contains(foundUsername))
                {
                    string hint = $"[CQ:at,qq={fromQq}] 请修改群名片，不要在用户名前后添加可以被用做用户名的字符，以免混淆。";
                    int firstIndex = inGroupName.IndexOf(foundUsername);
                    if (firstIndex != -1)
                    {
                        string recommendCard = inGroupName.Substring(0, firstIndex);
                        if (firstIndex != 0)
                            recommendCard += "|";
                        recommendCard += foundUsername;
                        if (firstIndex + foundUsername.Length < inGroupName.Length)
                        {
                            recommendCard += "|" + inGroupName.Substring(firstIndex + foundUsername.Length);
                        }
                        hint += Environment.NewLine + "建议群名片：" + Environment.NewLine;
                        hint += recommendCard;
                        qq.SendGroupMessageAsync(fromGroup, hint);
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("无法给出推荐名片");
                        sb.AppendLine("群名：" + inGroupName);
                        sb.AppendLine("用户名：" + foundUsername);
                        Logger.Log(sb.ToString());
                    }

                }
                else qq.SendGroupMessageAsync(fromGroup, $"[CQ:at,qq={fromQq}] 请修改群名片，必须包括正确的osu!用户名。数据库中您的名字是{foundUsername}，如改名请@我，如有错误请联系bleatingsheep。");
            }
            else
            {
                (string username, long findUid) = await CheckUsername(possibleUsernames);
                if (username == null) return;
                if (username == string.Empty)
                {
                    qq.SendGroupMessageAsync(fromGroup, $"[CQ:at,qq={fromQq}] 您尚未绑定osu!id，请将群名片改为osu!中的名字，直到提示您绑定成功。");
                    //+ Environment.NewLine + "!setid 您的id");
                }
                else
                {
                    database.CacheUsername(findUid, username);
                    database.Bind(fromQq, findUid, "Auto");
                    var success = await Int100ApiClient.BindQqAndOsuUid(fromQq, findUid);
                    qq.SendGroupMessageAsync(fromGroup, $"[CQ:at,qq={fromQq}] 自动绑定为{username}，{(success ? string.Empty : "但不知道是否成功，")}请发送“~”查询信息。如有错误请联系bleatingsheep。");
                    Logger.Log("自动绑定" + fromQq + username);
                    qq.SendGroupMessageAsync(fromGroup, $"!stats {findUid}");
                }
            }
        }

        /// <summary>
        /// 检查群名片是否包含osu的名字
        /// </summary>
        /// <param name="fromGroup">来自群组</param>
        /// <param name="fromQq">来自QQ</param>
        /// <param name="uid">osu! uid，如果未绑定，则为0</param>
        /// <returns></returns>
        private async Task<InGroupNameCheckResult> CheckInGroupName(string inGroupName, long uid)
        {
            var possibleUsernames = ParseUsername(inGroupName);
            if (possibleUsernames.Length == 0)
            {
                return InGroupNameCheckResult.NotContains;
            }

            if (uid != 0)
            {
                string foundUsername = await FindUsername(uid); //找到的用户名
                if (foundUsername == null) return InGroupNameCheckResult.Error; //查找失败
                if (possibleUsernames.Any(psb =>
                    psb.ToLowerInvariant() == foundUsername.ToLowerInvariant()
                )) return InGroupNameCheckResult.Qualified; //OK
                return inGroupName.IndexOf(foundUsername, StringComparison.InvariantCultureIgnoreCase) != -1 ?
                    InGroupNameCheckResult.IsSubstring :
                    InGroupNameCheckResult.NotOwner;
            }
            else
            {
                return InGroupNameCheckResult.NeverBind;
            }
        }

        /// <summary>
        /// 通过群名片自动绑定
        /// </summary>
        /// <param name="qq"></param>
        /// <param name="inGroupName"></param>
        /// <returns>是否成功；如果因为群名片不好返回<c>false</c>；网络问题返回<c>null</c></returns>
        private async Task<(bool? success, string username, long uid)> AutoBind(long group, long qq)
        {
            string inGroupName;
            var memberInfo = this.qq.GetGroupMemberInfo(group, qq);
            if (memberInfo == null)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("找不到群员信息");
                sb.AppendLine(group.ToString() + ", " + qq.ToString());
                Logger.Log(sb.ToString());
                return (null, null, 0);
            }
            inGroupName = memberInfo.InGroupName;

            var possibleUsernames = ParseUsername(inGroupName);

            (string username, long findUid) = await CheckUsername(possibleUsernames);
            if (username == null) return (null, null, 0);
            if (username == string.Empty)
            {
                return (false, username, 0);
            }
            else
            {
                database.CacheUsername(findUid, username);
                database.Bind(qq, findUid, "Auto");
                var success = await Int100ApiClient.BindQqAndOsuUid(qq, findUid);
                Logger.Log("自动绑定" + qq + username);
                return (true, username, findUid);
            }
        }

        /// <summary>
        /// 群名片检查结果
        /// </summary>
        private enum InGroupNameCheckResult
        {
            /// <summary>
            /// 此人从未绑定过osu!ID
            /// </summary>
            NeverBind = 5,
            /// <summary>
            /// 检查过程中出现错误
            /// </summary>
            Error = 0,
            /// <summary>
            /// 不包含任何合法群名片
            /// </summary>
            NotContains = 1,
            /// <summary>
            /// 包含至少一个合法的群名片，但是没有他正在使用的
            /// </summary>
            NotOwner = 2,
            /// <summary>
            /// 包含群名片，但是前后有其他字符
            /// </summary>
            IsSubstring = 3,
            /// <summary>
            /// 合格，或者自动绑定后合格
            /// </summary>
            Qualified = 4
        }

        /// <summary>
        /// （执行）检查PP
        /// </summary>
        /// <param name="fromGroup"></param>
        /// <param name="fromQq"></param>
        /// <param name="uid"></param>
        /// <returns></returns>
        private async Task CheckIfPPOverLimit(long fromGroup, long fromQq, long uid)
        {
            var users = await apiClient.GetUserAsync(uid.ToString(), OsuApiClient.UsernameType.User_id);
            if (users == null || !users.Any()) return;
            if (double.TryParse(users[0].pp_raw, out double pp))
                if (pp >= 3000)
                    qq.SendGroupMessageAsync(fromGroup, $"[CQ:at,qq={fromQq}] 您的PP超限，即将离开本群");
            //qq.SendGroupMessageAsync(fromGroup, $"[CQ:at,qq={fromQq}] 您的PP已经超过2600，如果超过3000，将离开本群");
        }

        private readonly OsuApiClient apiClient;

        private readonly long CurrentQq;
    }
}