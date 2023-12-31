import {
  SlashCommand,
  CommandContext,
  SlashCreator,
  CommandOptionType,
} from "slash-create";
import { SearchHandler } from "../search/SearchHandler";
import { InteractionType, SearchState } from "../misc/InteractionIdSerializer";
import { DibariSlashCommand } from "../misc/DibariSlashCommand";



export default class SearchCommand extends DibariSlashCommand {
  constructor(creator: SlashCreator) {
    super(creator, {
      name: "manga-search",
      description: "Searches MangaDex for the query provided. (searches titles, sorted by relevance.)",
      options: [
        {
          type: CommandOptionType.STRING,
          name: "query",
          description: "What manga are you after?",
          required: true,
        },
      ],
    });

    this.longDescription = "Searches MangaDex for the query provided. Is sorted the same way MangaDex sorts in their website (relevance, descending).";
    this.filePath = __filename;
  }

  async run(ctx: CommandContext) {
    await ctx.defer();

    const state: SearchState = {
      interactionType: InteractionType.Search_SearchForManga,
      query: ctx.options["query"],
      offset: 0
    }

    ctx.send(await SearchHandler.getNewMessageContents(state));
  }
}
