![enter image description here](https://tw.greywool.com/i/UgC1f.jpg)
 # Carrier - Banner and Torches Mod
This is the source code for Carrier mod. It's divided into two main folders.
**Source** - This folder contains all the C# files. It's not a whole VS Solution, you still need to add your own references(because they should be coming from game files) and create your own mod solution.
**Module Files** - This folder contains Prefabs, Items, and Translations. You should put this to your game Modules/Carrier in order to make it work as expected. If you did any modding before, you already know what to do.

Code itself is not rocket science, it's using the basics of Bannerlord base-code. Not using somewhat advanced topics like Reflection or not using Harmony. It's, however, doing some dirty tricks for some cases. I honestly didn't put too much thought into it apart from achieving what I wanted to achieve. It's also not "example code" and some parts that I wrote might be really ineffective - in such cases feel free to fix/improve the code base with your contribution.

Main Mod - [Carrier Banners and Torches in Nexusmods](https://www.nexusmods.com/mountandblade2bannerlord/mods/3354)

## Contribution
If you feel like you want to improve something or fix something, pull requests are welcomed and encouraged. I don't have any plans to keep it up-to-date all the time ( when TW releases a new version and such ) but if you do that and create a Pull-request, I can approve and update the mod. 

## About Mod
### Why?
I suggested having a separate soldier for banner carriers to dhkh1223 for <a  href="https://www.nexusmods.com/mountandblade2bannerlord/mods/3253">Raise your Banner mod</a> because it's causing balance issues when your high tier soldiers drop their shield for carrying the banner. He faced some exceptions while trying to implement the idea so I wanted to give it a go and see the issue. For that purpose, I created this in around one day or so ( meaning that don't expect something tested thoroughly ) Since it was working fine ( at least for my taste ) I said *why not release it as a mod* and here we are. Perhaps this can help future mods and future banner mods. I'm also releasing the source-code, you can use it anywhere you like. If you credit the source it would be appreciated but it's not a requirement.

#### Features
  It's adding bannermen to game. Although, unlike most of the banner mods, I wanted to give some functionality to their existence as well. Features goes as follow:
  
 - Every banner carrier has a certain radius which effects the morale of the troops around. This check happens in every 5 seconds, and it's limited to a certain morale limit. ( Example, in every 5 seconds, it checks troops around the radius and boosts morale by +10 if troop morale is below 40 ) And all these can be changed via Config.json 
 - When banner carrier dies, soldiers within the same radius explained above will get a morale decrease and if it's too low, they will eventually start to route.
 - When any of the banner carrier reaches the walls, you will get a message and this will immediately boost the morale of entire army to 100 once. This is one time thing and won't happen again if the second carrier reaches to walls again.
 - Equipment/look of banner carries are depending on the leader party's Tier. Meaning that, more prestigious lords will more heavy and noble-like banner carriers compared to other clans. 
 - Banner carriers can also carry torches - which comes in handy when you fight in night battles.
 - Nearly all of the things mentioned above can be configured and fine-tuned by you via Config.json
  
  All these features are tested only by default values and combination of certain 
#### Customization
You can customize several numbers and enable/disable some features. Just go to your Config.json. By default you should see this:

        {
      "Banner": {
    	  "AllowSiegeAttackers": true,
    	  "AllowSiegeDefenders": false,
    	  "AllowRaidAttackers": true,
    	  "AllowRaidDefenders": false,
    	  "AllowInHideout": false,
    	  "AllowMoraleBoostWhenBannermenReachWalls": true,
    	  "AllowBannermenReachedMessageAndSound": true,
    	  
    	  "MoraleRadius": 20,
    	  "MoraleDropWhenBannermanKilled": 15,
    	  "MaximumMoraleWhenAroundAllyBannerman": 40,
    	  "PerInfantry": 5,
    	  "PerCavalry": 10,
    	  "PerArcher": 5,
    	  "PerHorseArcher": 10,
    	  "PerHeavyCavalry": 5,
    	  "PerHeavyInfantry": 5,
    	  "PerSkirmisher": 5,
    	  "PerLightCavalry": 10
      },
      "Extra": {
    	  "UseTierBasedBannerman": true,
    	  "AlsoUseTorchAtNight": true,
    	  "AllowNonNobleArmiesToCarryBanner": false
      }
    }
Most of them are self explanatory I think. If you have questions you can ask from nexusmods. You can't, however, have torches without banners. It's not implemented yet. 

##### Save Game
Although I haven't  tested, most likely that it's not save-game compatitible since I'm adding a new soldier type. You won't be able to garrison these soldiers under normal circumstances though but still it can cause issues. You can plug-in and play with your existing game though. New game is not required. 

**More stable and different solution**
If you want something more stable and more up-to-date guarenteed, you should check  dhkh1223's mod - <a  href="https://www.nexusmods.com/mountandblade2bannerlord/mods/3253">Raise your Banner mod</a> he also has Raise your torch mod which brings torches to game as well. After checking this source code, he will probably bring same functionality to his mod as well. 