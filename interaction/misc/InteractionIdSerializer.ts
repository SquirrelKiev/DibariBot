import { SeriesIdentifier } from "../manga/MangaTypes";

export interface NavigateState {
  interactionType:
    | InteractionType.Manga_BackChapter
    | InteractionType.Manga_BackPage
    | InteractionType.Manga_ForwardChapter
    | InteractionType.Manga_ForwardPage
    | InteractionType.Manga_Open;
  identifier: SeriesIdentifier;
  chapter: string;
  page: number;
}

export interface SearchState {
  interactionType:
    | InteractionType.Search_SearchForManga
    | InteractionType.Search_ForwardPage
    | InteractionType.Search_BackPage;
  query?: string;
  offset: number;
}

export interface DebugSwitchPageState {
  interactionType: InteractionType.Debug_SwitchPage;
  page: DebugPageType;
}

export enum DebugPageType {
  Main,
  Cache,
}

export enum InteractionType {
  None,
  Close,
  Manga_BackPage,
  Manga_ForwardPage,
  Manga_BackChapter,
  Manga_ForwardChapter,
  Manga_Open,
  Search_SearchForManga,
  Search_BackPage,
  Search_ForwardPage,
  Search_SelectManga,
  Debug_SwitchPage,
  Debug_ClearCache,
}

export class InteractionIdSerializer {
  static encodeMangaNavigate(state: NavigateState): string {
    let customIdString = `${state.interactionType}|${state.identifier.platform}|${state.identifier.series}|${state.chapter}|${state.page}`;

    if (customIdString.length > 100) {
      throw new Error("customIdString exceeds 100 characters");
    }

    return customIdString;
  }

  static decodeMangaNavigate(customIdString: string): NavigateState {
    let data = customIdString.split("|");

    if (data.length !== 5) {
      throw new Error("Invalid customIdString format");
    }

    return {
      interactionType: parseInt(data[0]),
      identifier: {
        platform: data[1],
        series: data[2],
      },
      chapter: data[3],
      page: parseInt(data[4]),
    };
  }

  static encodeSearchSelect(mangaId: string): string {
    let customIdString = `${InteractionType.Search_SelectManga}|${mangaId}`;

    if (customIdString.length > 100) {
      throw new Error("customIdString exceeds 100 characters");
    }

    return customIdString;
  }

  static decodeSearchSelect(customIdString: string): string {
    let data = customIdString.split("|");

    if (data.length !== 2) {
      throw new Error("Invalid customIdString format");
    }

    return data[1];
  }

  static encodeSearchNavigate(state: SearchState): string {
    let customIdString = `${state.interactionType}|${state.query}|${state.offset}`;

    if (customIdString.length > 100) {
      throw new Error("customIdString exceeds 100 characters somehow");
    }

    return customIdString;
  }

  static decodeSearchNavigate(customIdString: string): SearchState {
    let data = customIdString.split("|");

    if (data.length !== 3) {
      throw new Error("Invalid customIdString format");
    }

    return {
      interactionType: parseInt(data[0]),
      query: data[1],
      offset: parseInt(data[2]),
    };
  }

  static encodeDebugSwitchPage(state: DebugSwitchPageState): string {
    let customIdString = `${state.interactionType}|${state.page}`;

    if (customIdString.length > 100) {
      throw new Error("customIdString exceeds 100 characters somehow");
    }

    return customIdString;
  }

  static decodeDebugSwitchPage(customIdString: string): DebugSwitchPageState {
    let data = customIdString.split("|");

    if (data.length !== 2) {
      throw new Error("Invalid customIdString format");
    }

    return {
      interactionType: parseInt(data[0]),
      page: parseInt(data[1]),
    };
  }

  static getInteractionType(customIdString: string): InteractionType {
    let data = customIdString.split("|");

    let interactionType: InteractionType = parseInt(data[0]);

    return interactionType;
  }
}
