// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Nether.Data.Leaderboard;
using Nether.Integration.Analytics;
using Nether.Web.Features.Leaderboard.Configuration;
using Nether.Web.Utilities;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Nether.Web.Features.Leaderboard
{
    [Route("api/leaderboard")]
    public class LeaderboardController : Controller
    {
        private readonly ILeaderboardStore _store;
        private readonly IAnalyticsIntegrationClient _analyticsIntegrationClient;

        public LeaderboardController(ILeaderboardStore store, IAnalyticsIntegrationClient analyticsIntegrationClient)
        {
            _store = store;
            _analyticsIntegrationClient = analyticsIntegrationClient;
        }

        //TODO: Add versioning support
        //TODO: Add authentication


        [Authorize(Roles = "player")]
        [HttpGet("{leaderboardname}")]
        public async Task<ActionResult> Get(string leaderboardname) //TODO: add swagger annotations for response shape
        {
            //TODO
            var gamerTag = User.GetGamerTag();

            List<GameScore> scores = new List<GameScore>();

            // currently hard coded leaderboard types
            if (String.IsNullOrEmpty(leaderboardname) || !Configuration.Configuration.LeaderboardConfiguration.ContainsKey(leaderboardname))
            {
                // default
                scores = await _store.GetAllHighScoresAsync();
            }
            else
            {
                LeaderboardConfig config = Configuration.Configuration.LeaderboardConfiguration[leaderboardname];
                if (config.AroundMe)
                {
                    scores = await _store.GetScoresAroundMeAsync(gamerTag, config.Radius);
                }
                else
                {
                    // in case top = 0, the implementation should lead to GetAllHighScores
                    scores = await _store.GetTopHighScoresAsync(config.Top);
                }
            }

            // Format response model
            var resultModel = new LeaderboardGetResponseModel
            {
                LeaderboardEntries = scores.Select(s => (LeaderboardGetResponseModel.LeaderboardEntry)s).ToList()
            };

            // Return result
            return Ok(resultModel);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult> Post([FromBody]LeaderboardPostRequestModel score)
        {
            //TODO: Make validation more sophisticated, perhaps some games want/need negative scores
            // Validate input
            if (score.Score < 0)
            {
                // TODO log
                return BadRequest(); //TODO: return error info in body
            }

            //TODO: Handle exceptions and retries
            var gamerTag = User.GetGamerTag();
            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                // TODO log
                return BadRequest(); //TODO: return error info in body
            }

            // Save score and call analytics in parallel
            await Task.WhenAll(
                _store.SaveScoreAsync(new GameScore
                {
                    GamerTag = gamerTag,
                    Country = score.Country,
                    CustomTag = score.CustomTag,
                    Score = score.Score
                }),
                _analyticsIntegrationClient.SendGameEventAsync(new ScoreAchieved
                {
                    GamerTag = gamerTag,
                    UtcDateTime = DateTime.UtcNow,
                    Score = score.Score
                }));

            // Return result
            return Ok();
        }
    }
}

