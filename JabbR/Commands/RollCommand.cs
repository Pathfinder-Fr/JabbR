using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JabbR.Models;

namespace JabbR.Commands
{
	using Microsoft.AspNet.SignalR;

	[Command("roll", "Roll_CommandInfo", "formula", "user")]
    public class RollCommand : UserCommand
    {
        private static readonly Regex regex = new Regex(@"^(?<Formula>(?<Roll>\d+(d\d+)?)(\s*(?<Roll>[\+\-]\d+(d\d+)?))*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public override void Execute(CommandContext context, CallerContext callerContext, ChatUser callingUser, string[] args)
        {
            var room = context.Repository.VerifyUserRoom(context.Cache, callingUser, callerContext.RoomName);

            if (args.Length == 0)
            {
                throw new HubException("You must provide a dice formula (eg. 4d6+1d4+3)");
            }

            ChatUser target = null;
            var formula = args.Last();

            if (args[0][0] == '@')
            {
                target = context.Repository.VerifyUser(args[0]);
            }

            var label = string.Join(" ", args.Skip(target != null ? 1 : 0).Take(args.Length - (target != null ? 1 : 0) - 1));

            var match = regex.Match(formula);

            var result = 0;
            var rolls = new List<int>();

            if (!match.Success)
            {
                throw new HubException(string.Format("Unknown formula : {0}", formula));
            }

            var r = new Random();
            foreach (Capture dice in match.Groups["Roll"].Captures)
            {
                var diceFormula = dice.Value;
                var hasDice = diceFormula.IndexOf('d') != -1;
                var diceFormulaParts = diceFormula.Split('d');
                int roll;
                if (diceFormulaParts.Length == 1)
                {
                    if (hasDice)
                    {
                        // "d20"                            
                        roll = r.Next(1, int.Parse(diceFormulaParts[0]));
                        rolls.Add(roll);
                        result += roll;
                    }
                    else
                    {
                        // "10"
                        result += int.Parse(diceFormulaParts[0]);
                    }
                }
                else if (diceFormulaParts.Length == 2)
                {
                    var count = int.Parse(diceFormulaParts[0]);

                    var coef = 1;
                    if (count < 0)
                    {
                        count = -count;
                        coef = -1;
                    }

                    for (var i = 0; i < count; i++)
                    {
                        roll = r.Next(1, int.Parse(diceFormulaParts[1]));
                        rolls.Add(roll);
                        result += roll * coef;
                    }
                }
            }

            string content;
            if (!string.IsNullOrWhiteSpace(label))
            {
                content = string.Format("{0} : {4} rolls {1} = {2} ({3})", label, formula, result, string.Join(", ", rolls), callingUser.Name);
            }
            else
            {
                content = string.Format("{3} rolls {0} = {1} ({2})", formula, result, string.Join(", ", rolls), callingUser.Name);
            }

            if (target == null)
            {
                // room
                foreach (var user in room.Users)
                {
                    context.NotificationService.PostNotification(room, user, content);
                }
            }
            else
            {
                // private
                if (target != callingUser)
                {
                    context.NotificationService.PostNotification(room, callingUser, content);
                }

                context.NotificationService.PostNotification(room, target, content);
            }
        }
    }
}